using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Encryption;
using System.Security.Cryptography.X509Certificates;

// IN VALUES HERE!
const string PetName = "solid-cat";
const string MdbPassword = "password123";
const string AppUser = "app_user";
const string CaPath = "/etc/pki/tls/certs/ca.cert";
const string PemPath = "/home/ec2-user/server.pem";

// Obviously this should not be hardcoded
const string connection_string = $"mongodb://{AppUser}:{MdbPassword}@csfle-mongodb-{PetName}.mdbtraining.net/?serverSelectionTimeoutMS=5000&tls=true&tlsCAFile={CaPath}";

// Declare or key vault namespce
const string keyvault_db = "__encryption";
const string keyvault_coll = "__keyVault";
var keyvault_namespace = new CollectionNamespace(keyvault_db, keyvault_coll);

// declare our key provider type
const string provider = "kmip";

// declare our key provider attributes
var kms_provider = new Dictionary<string, IReadOnlyDictionary<string, object>>();
var provider_settings = new Dictionary<string, object>
{
    { "endpoint", $"csfle-kmip-{PetName}.mdbtraining.net" }
};
kms_provider.Add(provider, provider_settings);

// declare our database and collection
const string encrypted_db_name = "companyData";
const string encrypted_coll_name = "employee";

// instantiate our MongoDB Client object
var client = new MongoClient(connection_string);

// instantiate our ClientEncryption object
var cert = new X509Certificate("/home/ec2-user/server.pem");
// var sss = cert.ToString(true);
var tls_options = new SslSettings();
tls_options.ClientCertificates = new X509Certificate[] { cert };
var kms_tls_options = new Dictionary<string, SslSettings> { { provider, tls_options } };
var client_encryption_options = new ClientEncryptionOptions(client, keyvault_namespace, kms_provider, kms_tls_options);
var client_encryption = new ClientEncryption(client_encryption_options);

// var dbs = client.ListDatabaseNames();
// dbs.ForEachAsync(db => System.Console.WriteLine(db.ToString()));

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
    { "salary", 999999.99m },
    { "taxIdentifier", "78SD20NN001" },
    {
        "role", new BsonArray { "CTO" }
    }
};

try
{
    // retrieve the DEK UUID
    var data_key_id_1 = (await client_encryption.GetKeyByAlternateKeyNameAsync("dataKey1"))["_id"];
    // Put code here to find the _id of the DEK we created previously
    if (data_key_id_1.IsBsonNull)
    {
        System.Console.WriteLine("Failed to find DEK");
        return;
    }

    // WRITE CODE HERE TO ENCRYPT THE APPROPRIATE FIELDS
    // Don't forget to handle to event of name.otherNames being null

    // Do deterministic fields
    // payload["name"]["firstName"] = # Put code here to encrypt the data
    // payload["name"]["lastName"] = # Put code here to encrypt the data
    var deterministicEncryptOptions = new EncryptOptions(EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Deterministic, keyId: data_key_id_1.AsGuid);
    payload["name"]["firstName"] = await client_encryption.EncryptAsync(payload["name"]["firstName"], deterministicEncryptOptions);
    payload["name"]["firstName"] = await client_encryption.EncryptAsync(payload["name"]["firstName"], deterministicEncryptOptions);

    // Do random fields
    // if payload["name"]["otherNames"] is None:
    //     # put code here to delete this field if None
    // else:
    //     payload["name"]["otherNames"] = # Put code here to encrypt the data
    // payload["address"] = # Put code here to encrypt the data
    // payload["dob"] = # Put code here to encrypt the data
    // payload["phoneNumber"] = # Put code here to encrypt the data
    // payload["salary"] = # Put code here to encrypt the data
    // payload["taxIdentifier"] = # Put code here to encrypt the data
    var randomEncryptOptions = new EncryptOptions(EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Random, keyId: data_key_id_1.AsGuid);
    if (payload["name"]["otherNames"].IsBsonNull)
    {
        payload["name"].AsBsonDocument.Remove("otherNames");
    }
    else
    {
        payload["name"]["otherNames"] = await client_encryption.EncryptAsync(payload["name"]["otherNames"], randomEncryptOptions);
    }
    payload["address"] = await client_encryption.EncryptAsync(payload["address"], randomEncryptOptions);
    payload["dob"] = await client_encryption.EncryptAsync(payload["dob"], randomEncryptOptions);
    payload["phoneNumber"] = await client_encryption.EncryptAsync(payload["phoneNumber"], randomEncryptOptions);
    payload["salary"] = await client_encryption.EncryptAsync(payload["salary"], randomEncryptOptions);
    payload["taxIdentifier"] = await client_encryption.EncryptAsync(payload["taxIdentifier"], randomEncryptOptions);


    // Test if the data is encrypted
    foreach (var data in new[] { payload["name"]["firstName"], payload["name"]["lastName"], payload["address"], payload["dob"], payload["phoneNumber"], payload["salary"], payload["taxIdentifier"] })
    {
        if (!data.IsBsonBinaryData && data["subtype"].AsInt32 != 6)
        {
            System.Console.WriteLine("Data is not encrypted");
            Environment.Exit(1);
        }
    }
}
catch (MongoEncryptionException ex)
{
    System.Console.WriteLine($"Encryption error {ex}");
    return;
}

System.Console.WriteLine(payload);

await client.GetDatabase(encrypted_db_name).GetCollection<BsonDocument>(encrypted_coll_name).InsertOneAsync(payload);

System.Console.WriteLine(payload["_id"]);
