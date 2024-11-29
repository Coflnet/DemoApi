PACKAGE_NAME=Coflnet.Whisper

docker run --rm -v "${PWD}:/local" --network host -u $(id -u ${USER}):$(id -g ${USER})  openapitools/openapi-generator-cli generate \
-i http://localhost:9000/openapi.json \
-g csharp \
-o /local/out --additional-properties=packageName=$PACKAGE_NAME,targetFramework=net8.0

cp -r out/src/$PACKAGE_NAME/* .
rm $PACKAGE_NAME.csproj

rm -rf out