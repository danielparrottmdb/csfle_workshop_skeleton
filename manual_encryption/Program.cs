using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Encryption;
using System.Security.Cryptography.X509Certificates;

// TODO: IN VALUES HERE!
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
var keyvault_namespace = new CollectionNamespace(keyvaultDb, keyvaultColl);

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
var client = new MongoClient(connectionString);

// Instantiate our ClientEncryption object
var tlsOptions = new SslSettings { ClientCertificates = new [] { new X509Certificate(pkcs12Path) } };
var kmsTlsOptions = new Dictionary<string, SslSettings> { { provider, tlsOptions } };
var clientEncryptionOptions = new ClientEncryptionOptions(client, keyvault_namespace, kmsProvider, kmsTlsOptions);
var clientEncryption = new ClientEncryption(clientEncryptionOptions);

var payload = new BsonDocument
{
    {
        "name", new BsonDocument
        {
            { "firstName", "Manish" },
            { "lastName", "Engineer" },
            { "otherNames", BsonNull.Value },
        }
    },
    {
        "address", new BsonDocument
        {
            { "streetAddress", "1 Bson Street" },
            { "suburbCounty", "Mongoville" },
            { "stateProvince", "Victoria" },
            { "zipPostcode", "3999" },
            { "country", "Oz" }
        }
    },
    { "dob", new DateTime(1980, 10, 10) },
    { "phoneNumber", "1800MONGO" },
    { "salary", 999999.99 },
    { "taxIdentifier", "78SD20NN001" },
    {
        "role", new BsonArray { "CTO" }
    }
};

// Retrieve the DEK UUID
var dataKeyId_1 = ;// TODO: Put code here to find the _id of the DEK we created previously
if (dataKeyId_1.IsBsonNull)
{
    Console.WriteLine("Failed to find DEK");
    return;
}

// WRITE CODE HERE TO ENCRYPT THE APPROPRIATE FIELDS
// Don't forget to handle to event of name.otherNames being null

// Do deterministic fields
payload["name"]["firstName"] = ; // TODO: Put code here to encrypt the data
payload["name"]["lastName"] = ; // TODO: Put code here to encrypt the data

// Do random fields

var randomEncryptOptions = new EncryptOptions(EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Random, keyId: dataKeyId_1.AsGuid);
if (payload["name"]["otherNames"].IsBsonNull)
{
    // TODO:  put code here to delete this field if null
}
else
{
    payload["name"]["otherNames"] = ; // TODO: Put code here to encrypt the data
}
payload["address"] = ; // TODO: Put code here to encrypt the data
payload["dob"] = ; // TODO: Put code here to encrypt the data
payload["phoneNumber"] = ; // TODO: Put code here to encrypt the data
payload["salary"] = ; // TODO: Put code here to encrypt the data
payload["taxIdentifier"] = ; // TODO: Put code here to encrypt the data

// Test if the data is encrypted
foreach (var data in new[] { payload["name"]["firstName"], payload["name"]["lastName"], payload["address"], payload["dob"], payload["phoneNumber"], payload["salary"], payload["taxIdentifier"] })
{
    if (data.BsonType != BsonType.Binary || data.AsBsonBinaryData.SubType != BsonBinarySubType.Encrypted)
    {
        Console.WriteLine("Data is not encrypted");
        return;
    }
}

Console.WriteLine(payload);
await client.GetDatabase(encryptedDbName).GetCollection<BsonDocument>(encryptedCollName).InsertOneAsync(payload);
Console.WriteLine(payload["_id"]);
