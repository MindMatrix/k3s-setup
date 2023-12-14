#!/bin/bash

BUILD_IMAGE=docker-hosted.gladeos.net/amp/proxy
BUILD_BRANCH=$(git rev-parse --abbrev-ref HEAD)
BUILD_NUMBER=$(date '+%Y.%m.%d.%H%M')
BUILD_COMMIT=$(git rev-parse --verify HEAD)
BUILD_SHORTCOMMIT=${BUILD_COMMIT:0:6}
echo "Branch: $BUILD_BRANCH"
echo "Version: $BUILD_NUMBER"
echo "Commit: $BUILD_COMMIT"

dotnet publish src --os linux --arch x64 -c Release -p:ContainerImageTag=$BUILD_NUMBER -P:ContainerRepository=$BUILD_IMAGE
if [ $? -ne 0 ]; then
    echo "Build failed."
else
    docker push $BUILD_IMAGE:$BUILD_NUMBER
    #kubectl set image deployment/proxy proxy=mindmatrix/proxy:$BUILD_NUMBER
fi
