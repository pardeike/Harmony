#!/bin/bash
dotnet clean
dotnet restore
dotnet build -p:MonoModCoreVersion="" -c Release