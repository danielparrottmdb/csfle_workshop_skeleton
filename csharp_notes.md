Install .NET SDK 7.0
====================
> sudo rpm -Uvh https://packages.microsoft.com/config/centos/7/packages-microsoft-prod.rpm

> sudo yum install dotnet-sdk-7.0

Run the C# code 
===============
All C# projects consist of a single Program.cs, using the top-level code feature. There is a program.csproj file at the root 
which can be used for running all C# samples. Simply copy the program.csproj file to the directory with the relevant Program.cs
file and give the command
> dotnet run

Create new .NET project
=======================
> dotnet new console

Important! Include the version parameter since later versions will not work with Amazon Linux 2 (GLIBC version issues)
> dotnet add package MongoDB.Driver -v 2.18.0

Special hacks for .NET
======================
Export server.pem to pkcs12 format required by .NET
> openssl pkcs12 -export -out "/home/ec2-user/server.pkcs12" -in "/home/ec2-user/server.pem" -name "kmipcert"

Might need to do this (Amazon Linux 2023 has this issue)
> cp /usr/lib64/libdl.so /home/ec2-user/csfle_workshop_skeleton/&lt;folder&gt;/bin/Debug/net7.0/runtimes/linux/native/libdl.so

