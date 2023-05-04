Install .NET SDK 7.0
====================
> sudo rpm -Uvh https://packages.microsoft.com/config/centos/7/packages-microsoft-prod.rpm

> sudo yum install dotnet-sdk-7.0

Connect with mongosh
> mongosh "mongodb://mongoadmin:passwordone@csfle-mongodb-solid-cat.mdbtraining.net:27017/?replicaSet=rs0" --tls --tlsCAFile /etc/pki/tls/certs/ca.cert

Create new .NET project
=======================
> dotnet new console

> dotnet add package MongoDB.Driver -v 2.18.0

Special hacks for .NET
======================
Export server.pem to pkcs12 format required by .NET
> openssl pkcs12 -export -out "/home/ec2-user/server.pkcs12" -in "/home/ec2-user/server.pem" -name "kmipcert"

Might need to do this (Amazon Linux 2023 has this issue)
> cp /usr/lib64/libdl.so /home/ec2-user/csfle_workshop_skeleton/manual_encryption/bin/Debug/net7.0/runtimes/linux/native/libdl.so


Random Feedback on slides
=========================

Slide 26 & 27 - Switch order

Slide 30 -"mongocryptd is ONLY required for automatic field level encryption and if crypt_shared is not use. It performs the following:"  A bit confusing - "If you are not using crypt_shared then mongocryptd is ONLY required..."

Everywhere - the "// <--" makes me think I need to change something, skip the "<--"