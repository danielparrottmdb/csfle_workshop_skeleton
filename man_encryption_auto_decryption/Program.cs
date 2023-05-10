using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Encryption;
using System.Security.Cryptography.X509Certificates;

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
var tlsOptions = new SslSettings { ClientCertificates = new[] { new X509Certificate(pkcs12Path) } };
var kmsTlsOptions = new Dictionary<string, SslSettings> { { provider, tlsOptions } };
var clientEncryptionOptions = new ClientEncryptionOptions(client, keyvaultNamespace, kmsProvider, kmsTlsOptions);
var clientEncryption = new ClientEncryption(clientEncryptionOptions);

var extraOptions = new Dictionary<string, object>()
{
    { "cryptSharedLibPath", "/lib/mongo_crypt_v1.so"},
    { "cryptSharedLibRequired", true },
    { "mongocryptdBypassSpawn", true }
};
var autoEncryption = new AutoEncryptionOptions(
    kmsProviders: kmsProvider,
    keyVaultNamespace: keyvaultNamespace,
    extraOptions: extraOptions,
    tlsOptions: kmsTlsOptions);
var encryptedClient = MdbClient(connectionString, autoEncryption);

var payload = new BsonDocument
{
    {
        "name", new BsonDocument
        {
            { "firstName", "Poorna" },
            { "lastName", "Muggle" },
            { "otherNames", BsonNull.Value },
        }
    },
    {
        "address", new BsonDocument
        {
            { "streetAddress", "29 Bson Street" },
            { "suburbCounty", "Mongoville" },
            { "stateProvince", "Victoria" },
            { "zipPostcode", "3999" },
            { "country", "Oz" }
        }
    },
    { "dob", new DateTime(1999, 1, 12) },
    { "phoneNumber", "1800MONGO" },
    { "salary", 999999.99m },
    { "taxIdentifier", "78SDSSWN001" },
    { "role", new BsonArray { "CE" } }
};

// Retrieve the DEK UUID
var dataKeyId_1 = (await clientEncryption.GetKeyByAlternateKeyNameAsync("dataKey1"))["_id"];
if (dataKeyId_1.IsBsonNull)
{
    Console.WriteLine("Failed to find DEK");
    return;
}

// Do deterministic fields
var deterministicEncryptOptions = new EncryptOptions(EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Deterministic, keyId: dataKeyId_1.AsGuid);
payload["name"]["firstName"] = await clientEncryption.EncryptAsync(payload["name"]["firstName"], deterministicEncryptOptions);
payload["name"]["lastName"] = await clientEncryption.EncryptAsync(payload["name"]["lastName"], deterministicEncryptOptions);

// Do random fields
var randomEncryptOptions = new EncryptOptions(EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Random, keyId: dataKeyId_1.AsGuid);
if (payload["name"]["otherNames"].IsBsonNull)
{
    payload["name"].AsBsonDocument.Remove("otherNames");
}
else
{
    payload["name"]["otherNames"] = await clientEncryption.EncryptAsync(payload["name"]["otherNames"], randomEncryptOptions);
}
payload["address"] = await clientEncryption.EncryptAsync(payload["address"], randomEncryptOptions);
payload["dob"] = await clientEncryption.EncryptAsync(payload["dob"], randomEncryptOptions);
payload["phoneNumber"] = await clientEncryption.EncryptAsync(payload["phoneNumber"], randomEncryptOptions);
payload["salary"] = await clientEncryption.EncryptAsync(payload["salary"], randomEncryptOptions);
payload["taxIdentifier"] = await clientEncryption.EncryptAsync(payload["taxIdentifier"], randomEncryptOptions);

// Test if the data is encrypted
foreach (var data in new[] { payload["name"]["firstName"], payload["name"]["lastName"], payload["address"], payload["dob"], payload["phoneNumber"], payload["salary"], payload["taxIdentifier"] })
{
    if (data.BsonType != BsonType.Binary || data.AsBsonBinaryData.SubType != BsonBinarySubType.Encrypted)
    {
        Console.WriteLine("Data is not encrypted");
        return;
    }
}

await client.GetDatabase(encryptedDbName).GetCollection<BsonDocument>(encryptedCollName).InsertOneAsync(payload);
Console.WriteLine(payload["_id"]);

// Encryped data to query
var encryptedName = clientEncryption.Encrypt("Poorna", deterministicEncryptOptions);

// WRITE YOUR QUERY HERE FOR AUTODECRYPTION. REMEMBER WHICH CLIENT TO USE!
var filter = Builders<BsonDocument>.Filter.Eq(d => d["name.firstName"], encryptedName);
var encryptedDoc = await (await encryptedClient.GetDatabase(encryptedDbName).GetCollection<BsonDocument>(encryptedCollName).FindAsync(filter)).FirstOrDefaultAsync<BsonDocument>();
Console.WriteLine(encryptedDoc);

static MongoClient MdbClient(string connectionString, AutoEncryptionOptions? options = null)
{
    var settings = MongoClientSettings.FromConnectionString(connectionString);
    if (options != null) settings.AutoEncryptionOptions = options;

    return new MongoClient(settings);
}
