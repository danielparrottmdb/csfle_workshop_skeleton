using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Encryption;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

// IN VALUES HERE!
const string PETNAME = "solid-cat";
const string MDB_PASSWORD = "password123";

const string appUser = "app_user";
const string caPath = "/etc/pki/tls/certs/ca.cert";

// Note that the .NET driver requires the certificate to be in PKCS12 format. You can convert
// the file /home/ec2-user/server.pem into PKCS12 with the command
// openssl pkcs12 -export -out "/home/ec2-user/server.pkcs12" -in "/home/ec2-user/server.pem" -name "kmipcert"
const string pkcs12Path = "/home/ec2-user/server.pkcs12";

// Obviously this should not be hardcoded
const string connectionString = $"mongodb://{appUser}:{MDB_PASSWORD}@csfle-mongodb-{PETNAME}.mdbtraining.net/?serverSelectionTimeoutMS=5000&tls=true&tlsCAFile={caPath}";

// Declare our key vault namespce
const string keyvaultDb = "__encryption";
const string keyvaultColl = "__keyVault";
var keyvaultNamespace = new CollectionNamespace(keyvaultDb, keyvaultColl);

// Declare our key provider type
const string provider = "kmip";

// Declare our key provider attributes
var providerSettings = new Dictionary<string, object>
{
    { "endpoint", $"csfle-kmip-{PETNAME}.mdbtraining.net" }
};
var kmsProvider = new Dictionary<string, IReadOnlyDictionary<string, object>>
{
    { provider, providerSettings }
};

// Declare our database and collection
const string encryptedDbName = "companyData";
const string encryptedCollName = "employee";

// Instantiate our MongoDB Client object
var client = MdbClient(connectionString);

// Instantiate our ClientEncryption object
var tlsOptions = new SslSettings { ClientCertificates = new [] { new X509Certificate(pkcs12Path) } };
var kmsTlsOptions = new Dictionary<string, SslSettings> { { provider, tlsOptions } };
var clientEncryptionOptions = new ClientEncryptionOptions(client, keyvaultNamespace, kmsProvider, kmsTlsOptions);
var clientEncryption = new ClientEncryption(clientEncryptionOptions);

var employeeId = $"{Random.Shared.NextDouble() * 100000d:00000}";
var (firstName, lastName) = GenerateName();

// PUT CODE HERE TO RETRIEVE OUR COMMON (our first) DEK:
var filter = Builders<BsonDocument>.Filter.Eq(d => d["keyAltNames"], "dataKey1");
var dataKeyId_1 = (await (await client.GetDatabase(keyvaultDb).GetCollection<BsonDocument>(keyvaultColl).FindAsync(filter)).FirstOrDefaultAsync<BsonDocument>())["_id"];
if (dataKeyId_1.IsBsonNull)
{
    Console.WriteLine("Common DEK missing");
    return;
}
var _ = await GetEmployeeKey(clientEncryption, employeeId, provider, "1");

var payload = new BsonDocument
{
    { "_id", employeeId },
    {
        "name", new BsonDocument
        {
            { "firstName", firstName },
            { "lastName", lastName },
            { "otherNames", BsonNull.Value },
        }
    },
    {
        "address", new BsonDocument
        {
            { "streetAddress", "3 Bson Street" },
            { "suburbCounty", "Mongoville" },
            { "stateProvince", "Victoria" },
            { "zipPostcode", "3999" },
            { "country", "Oz" }
        }
    },
    { "dob", new DateTime(1978, 10, 10) },
    { "phoneNumber", "1800MONGO" },
    { "salary", 999999.99 },
    { "taxIdentifier", "78SD20NN001" },
    { "role", new BsonArray { "CIO" } }
};

var schema = new BsonDocument
{
    { "bsonType", "object" },
    {
        "encryptMetadata", new BsonDocument {
            { "keyId", "/_id" },
            { "algorithm", "AEAD_AES_256_CBC_HMAC_SHA_512-Random" }
        }
    },
    {
        "properties", new BsonDocument { {
            "name", new BsonDocument {
                { "bsonType", "object"} ,
                {
                    "properties", new BsonDocument { {
                        "firstName", new BsonDocument { {
                            "encrypt", new BsonDocument {
                                { "keyId", new BsonArray { dataKeyId_1 } },
                                { "bsonType", "string" },
                                { "algorithm", "AEAD_AES_256_CBC_HMAC_SHA_512-Deterministic" }
                            }
                        } }
                    },
                    {
                        "lastName", new BsonDocument { {
                            "encrypt", new BsonDocument {
                                { "keyId", new BsonArray { dataKeyId_1 } },
                                { "bsonType", "string" },
                                { "algorithm", "AEAD_AES_256_CBC_HMAC_SHA_512-Deterministic" }
                            }
                        } }
                    },
                    {
                        "otherNames", new BsonDocument { {
                            "encrypt", new BsonDocument { { "bsonType", "string" } }
                        } }
                    } }
                } }
            },
            {
                "address", new BsonDocument { {
                    "encrypt", new BsonDocument { { "bsonType", "object" } }
                } }
            },
            {
                "dob", new BsonDocument { {
                    "encrypt", new BsonDocument { { "bsonType", "date" } }
                } }
            },
            {
                "phoneNumber", new BsonDocument { {
                    "encrypt", new BsonDocument { { "bsonType", "string" } }
                } }
            },
            {
                "salary", new BsonDocument { {
                    "encrypt", new BsonDocument { { "bsonType", "double" } }
                } }
            },
            {
                "taxIdentifier", new BsonDocument { {
                    "encrypt", new BsonDocument { { "bsonType", "string" } }
                } }
            }
        }
    }
};
var schemaMap = new Dictionary<string, BsonDocument> { {"companyData.employee", schema } };

var extraOptions = new Dictionary<string, object>()
{
    { "cryptSharedLibPath", "/lib/mongo_crypt_v1.so"},
    { "cryptSharedLibRequired", true },
    { "mongocryptdBypassSpawn", true }
};
var autoEncryption = new AutoEncryptionOptions(
    kmsProviders: kmsProvider,
    keyVaultNamespace: keyvaultNamespace,
    schemaMap: schemaMap,
    extraOptions: extraOptions,
    tlsOptions: kmsTlsOptions);

var encryptedClient = MdbClient(connectionString, autoEncryption);
var encryptedColl = encryptedClient.GetDatabase(encryptedDbName).GetCollection<BsonDocument>(encryptedCollName);

// remove `name.otherNames` if None because wwe cannot encrypt none
if (payload["name"]["otherNames"].IsBsonNull)
{
    payload["name"].AsBsonDocument.Remove("otherNames");
}

await encryptedColl.InsertOneAsync(payload);
Console.WriteLine(payload["_id"]);

var query = Builders<BsonDocument>.Filter;
var filter1 = query.And(query.Eq(d => d["name.firstName"], firstName), query.Eq(d => d["name.lastName"], lastName));
var result = await (await encryptedColl.FindAsync(filter1)).FirstOrDefaultAsync();
Console.WriteLine(result);

var filter2 = query.Eq(d => d["keyAltNames"], employeeId);
await client.GetDatabase(keyvaultDb).GetCollection<BsonDocument>(keyvaultColl).DeleteOneAsync(filter2);

await Task.Delay(60000); // One minute

var result1 = await (await encryptedColl.FindAsync(filter1)).FirstOrDefaultAsync();
System.Console.WriteLine(result1);

static MongoClient MdbClient(string connectionString, AutoEncryptionOptions? options = null)
{
    var settings = MongoClientSettings.FromConnectionString(connectionString);
    settings.AutoEncryptionOptions = options;

    return new MongoClient(settings);
}

static (string, string) GenerateName()
{
    string[] firstNames = {"John","Paul","Ringo","George"};
    string[] lastNames = {"Lennon","McCartney","Starr","Harrison"};
    var firstName = firstNames[Random.Shared.Next(0, firstNames.Length)];
    var lastName = lastNames[Random.Shared.Next(0, lastNames.Length)];

    return (firstName, lastName);
}

static async Task<Guid> GetEmployeeKey(ClientEncryption client, string altName, string providerName, string keyId)
{
    var employeeKey = await client.GetKeyByAlternateKeyNameAsync(altName);
    if (employeeKey is null)
    {
        return await client.CreateDataKeyAsync(
            providerName, 
            new DataKeyOptions(
                alternateKeyNames: new [] { altName }, 
                masterKey: new BsonDocument {
                    { "keyId", keyId },
                    { "endpoint", $"csfle-kmip-{PETNAME}.mdbtraining.net" }
                }
            ));
    }
    return employeeKey["_id"].AsGuid;
}

