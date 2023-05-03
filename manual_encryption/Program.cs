using MongoDB.Driver;
using MongoDB.Driver.Encryption;
using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;

// from bson.binary import STANDARD, Binary
// from bson.codec_options import CodecOptions
// from datetime import datetime
// from pymongo import MongoClient
// from pymongo.encryption import Algorithm
// from pymongo.encryption import ClientEncryption
// from pymongo.errors import EncryptionError, ServerSelectionTimeoutError, ConnectionFailure
// from urllib.parse import quote_plus
// import sys


// IN VALUES HERE!
const string PetName = "solid-cat";
const string MdbPassword = "";
const string AppUser = "app_user";
const string CaPath = "/etc/pki/tls/certs/ca.cert";
const string PemPath = "/home/ec2-user/server.pem";

// private MongoClient GetClient(string )
// def mdb_client(connection_string, auto_encryption_opts=None):
//   """ Returns a MongoDB client instance
  
//   Creates a  MongoDB client instance and tests the client via a `hello` to the server

//   Parameters
//   ------------
//     connection_string: string
//       MongoDB connection string URI containing username, password, host, port, tls, etc
//   Return
//   ------------
//     client: mongo.MongoClient
//       MongoDB client instance
//     err: error
//       Error message or None of successful
//   """

//   try:
//     client = MongoClient(connection_string)
//     client.admin.command('hello')
//     return client, None
//   except (ServerSelectionTimeoutError, ConnectionFailure) as e:
//     return None, f"Cannot connect to database, please check settings in config file: {e}"

// def main():

// Obviously this should not be hardcoded
const string connection_string = $"mongodb://{AppUser}:{MdbPassword}@csfle-mongodb-{PetName}.mdbtraining.net/?serverSelectionTimeoutMS=5000&tls=true&tlsCAFile={CaPath}&tlsPEMKeyFile={PemPath}";

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
var cert = new X509Certificate( "/home/ec2-user/server.pem");
var sss = cert.ToString(true);
var tls_options = new SslSettings();
tls_options.ClientCertificates = new X509Certificate[] { cert };
var kms_tls_options = new Dictionary<string, SslSettings> { { provider, tls_options } };
var client_encryption_options = new ClientEncryptionOptions(client, keyvault_namespace, kms_provider, kms_tls_options);
var client_encryption = new ClientEncryption(client_encryption_options);

var dbs = client.ListDatabaseNames();
//   payload = {
//     "name": {
//       "firstName": "Manish",
//       "lastName": "Engineer",
//       "otherNames": None,
//     },
//     "address": {
//       "streetAddress": "1 Bson Street",
//       "suburbCounty": "Mongoville",
//       "stateProvince": "Victoria",
//       "zipPostcode": "3999",
//       "country": "Oz"
//     },
//     "dob": datetime(1980, 10, 10),
//     "phoneNumber": "1800MONGO",
//     "salary": 999999.99,
//     "taxIdentifier": "78SD20NN001",
//     "role": [
//       "CTO"
//     ]
//   }

//   try:

// // retrieve the DEK UUID
//     data_key_id_1 = # Put code here to find the _id of the DEK we created previously
//     if data_key_id_1 is None:
//       print("Failed to find DEK")
//       sys.exit()

// // WRITE CODE HERE TO ENCRYPT THE APPROPRIATE FIELDS
// // Don't forget to handle to event of name.otherNames being null

// // Do deterministic fields
// payload["name"]["firstName"] = # Put code here to encrypt the data
// payload["name"]["lastName"] = # Put code here to encrypt the data

// // Do random fields
// if payload["name"]["otherNames"] is None:
//     # put code here to delete this field if None
// else:
//     payload["name"]["otherNames"] = # Put code here to encrypt the data
// payload["address"] = # Put code here to encrypt the data
// payload["dob"] = # Put code here to encrypt the data
// payload["phoneNumber"] = # Put code here to encrypt the data
// payload["salary"] = # Put code here to encrypt the data
// payload["taxIdentifier"] = # Put code here to encrypt the data


// // Test if the data is encrypted
// for data in [ payload["name"]["firstName"], payload["name"]["lastName"], payload["address"], payload["dob"], payload["phoneNumber"], payload["salary"], payload["taxIdentifier"]]:
//     if type(data) is not Binary and data.subtype != 6:
//     print("Data is not encrypted")
//     sys.exit(-1)

// if "otherNames" in payload["name"] and payload["name"]["otherNames"] is None:
//     print("None cannot be encrypted")
//     sys.exit(-1)

// except EncryptionError as e:
// print(f"Encryption error: {e}")


// print(payload)

// result = client[encrypted_db_name][encrypted_coll_name].insert_one(payload)

// print(result.inserted_id)

