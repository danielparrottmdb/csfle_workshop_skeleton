using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Encryption;
using System.Security.Cryptography.X509Certificates;

// IN VALUES HERE!
const string PETNAME = ;
const string MDB_PASSWORD = ;

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

// Retrieve the DEK UUID
var employeeKeyId = await GetEmployeeKey(clientEncryption, employeeId, provider, "1");

var payload = new BsonDocument
{
    { "_id", employeeId }, // We are using this as our keyAltName
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
            { "keyId",  "" }, // TODO: PUT APPROPRIATE CODE OR VARIABLE HERE INSTEAD OF ""
            { "algorithm", "AEAD_AES_256_CBC_HMAC_SHA_512-Random" }
        }
    },
    {
        "properties", new BsonDocument { {
            "name", new BsonDocument {
                { "bsonType", "object"} ,
                {
                    "properties", new BsonDocument { 
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

// ENCRYPT THE name.firstName and name.lastName here
var deterministicEncryptOptions = new EncryptOptions(EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Deterministic, keyId: dataKeyId_1.AsGuid);
var encFirstName = await clientEncryption.EncryptAsync(payload["name"]["firstName"], deterministicEncryptOptions);
var encLastName = await clientEncryption.EncryptAsync(payload["name"]["lastName"], deterministicEncryptOptions);
payload["name"]["firstName"] = encFirstName;
payload["name"]["lastName"] = encLastName;

foreach (var data in new[] { payload["name"]["firstName"], payload["name"]["lastName"] })
{
    if (data.BsonType != BsonType.Binary || data.AsBsonBinaryData.SubType != BsonBinarySubType.Encrypted)
    {
        Console.WriteLine("Data is not encrypted");
        return;
    }
}

// remove `name.otherNames` if None because we cannot encrypt null
if (payload["name"]["otherNames"].IsBsonNull)
{
    payload["name"].AsBsonDocument.Remove("otherNames");
}

await encryptedColl.InsertOneAsync(payload);
Console.WriteLine(payload["_id"]);

var query = Builders<BsonDocument>.Filter;
var filter1 = query.And(query.Eq(d => d["name.firstName"], encFirstName), query.Eq(d => d["name.lastName"], encLastName));
var result = await (await encryptedColl.FindAsync(filter1)).FirstOrDefaultAsync();
Console.WriteLine(result);

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
        return await client...;  // TODO: PUT CODE HERE TO CREATE THE NEW DEK
    }
    return employeeKey["_id"].AsGuid;
}

