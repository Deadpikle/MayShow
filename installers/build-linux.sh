#!/bin/bash

SRC_DIR="src" # user ran script from main folder
if [ ! -d "$SRC_DIR" ]; then
    SRC_DIR= "../src" # try 
fi
if [ ! -d "$SRC_DIR" ]; then
    echo "Please run from "installers" dir or from main repo directory"
    exit 1
fi
cd "$SRC_DIR"
echo "Building release for linux-x64..."
dotnet publish -c Release -r linux-x64 -p:StripSymbols=False -p:PublishAot=False
echo "Building release for linux-arm64..."
dotnet publish -c Release -r linux-arm64 -p:StripSymbols=False -p:PublishAot=False
# TODO: add automatic zipping and version number setting for ease of use