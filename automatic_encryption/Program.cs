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

// Retrieve the DEK UUID
var filter = Builders<BsonDocument>.Filter.Eq(d => d["keyAltNames"], "dataKey1");
var dataKeyId_1 = (await (await client.GetDatabase(keyvaultDb).GetCollection<BsonDocument>(keyvaultColl).FindAsync(filter)).FirstOrDefaultAsync<BsonDocument>())["_id"];
if (dataKeyId_1.IsBsonNull)
{
    Console.WriteLine("Failed to find DEK");
    return;
}

var schema = new BsonDocument
{
    { "bsonType", "object" },
    {
        "encryptMetadata", new BsonDocument {
            { "keyId", new BsonArray { dataKeyId_1 } },
            { "algorithm", "" } // TODO: PUT APPROPRIATE ALGORITHM HERE
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
                                { "bsonType", "string" },
                                { "algorithm", "AEAD_AES_256_CBC_HMAC_SHA_512-Deterministic" }
                            }
                        } }
                        // TODO: PUT MORE FIELDS IN HERE
                    }
                } }
            } }
            // TODO: COMPLETE THE REST OF THE SCHEMA MAP
        }
    }
};
var schemaMap = new Dictionary<string, BsonDocument> { {"companyData.employee", schema } };

var tlsOptions = new SslSettings { ClientCertificates = new[] { new X509Certificate(pkcs12Path) } };
var kmsTlsOptions = new Dictionary<string, SslSettings> { { provider, tlsOptions } };
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

var (firstName, lastName) = GenerateName();

var payload = new BsonDocument
{
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
            { "streetAddress", "2 Bson Street" },
            { "suburbCounty", "Mongoville" },
            { "stateProvince", "Victoria" },
            { "zipPostcode", "3999" },
            { "country", "Oz" }
        }
    },
    { "dob", new DateTime(1999, 1, 12) },
    { "phoneNumber", "1800MONGO" },
    { "salary", 999999.99 },
    { "taxIdentifier", "78SD20NN001" },
    { "role", new BsonArray { "CIO" } }
};

// Do deterministic fields
if (payload["name"]["otherNames"].IsBsonNull)
{
    payload["name"].AsBsonDocument.Remove("otherNames");
}

await encryptedClient.GetDatabase(encryptedDbName).GetCollection<BsonDocument>(encryptedCollName).InsertOneAsync(payload);
Console.WriteLine(payload["_id"]);

static MongoClient MdbClient(string connectionString, AutoEncryptionOptions? options = null)
{
    var settings = MongoClientSettings.FromConnectionString(connectionString);
    if (options != null) settings.AutoEncryptionOptions = options;

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