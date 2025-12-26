# koornik api

## Run from Koornik.API project directory

cd Koornik.API

## initialize user-secrets (one-time)

dotnet user-secrets init

## set connection string for local runs (dotnet run -> Host=localhost)

dotnet user-secrets set "ConnectionStrings:KoornikDB" "Host=localhost;Port=5432;Database=KoornikDB;Username=koornik_user;Password=pa$$w0rd;Timeout=15"

## optional: connection string to use when running API in Docker (Host=db)

dotnet user-secrets set "ConnectionStrings:KoornikDB" "Host=db;Port=5432;Database=KoornikDB;Username=koornik_user;Password=pa$$w0rd;Timeout=15"

## verify

dotnet user-secrets list

## ensure environment is Development when running locally

$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run
