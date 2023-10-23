#!/bin/bash

BUILD_SPACE=$(dirname "$(readlink -f "$0")")
BUILD_SPACE=${BUILD_SPACE::-14}
BUILD_OUTPUT="$BUILD_SPACE/Builds"
echo "Workspace: $BUILD_SPACE"
echo "Output: $BUILD_OUTPUT"

mkdir -p "$BUILD_OUTPUT"
echo "$BUILD_SPACE" > "$BUILD_OUTPUT/BUILD_SPACE.env"
echo "$BUILD_OUTPUT" > "$BUILD_OUTPUT/BUILD_OUTPUT.env"

BUILD_START=$(date)
BUILD_YEAR=$(date '+%Y')
echo "Year: $BUILD_YEAR"
echo $BUILD_YEAR > "$BUILD_OUTPUT/BUILD_YEAR.env"

BUILD_BRANCH=$(git rev-parse --abbrev-ref HEAD)
echo "Branch: $BUILD_BRANCH"
echo $BUILD_BRANCH > "$BUILD_OUTPUT/BUILD_BRANCH.env"

BUILD_NUMBER=$(date '+%Y.%m.%d.%H%M')
echo "Version: $BUILD_NUMBER"
echo $BUILD_NUMBER > "$BUILD_OUTPUT/BUILD_NUMBER.env"

BUILD_COMMIT=$(git rev-parse --verify HEAD)
BUILD_SHORTCOMMIT=${BUILD_COMMIT:0:6}
echo "Commit: $BUILD_COMMIT"
echo $BUILD_COMMIT > "$BUILD_OUTPUT/BUILD_COMMIT.env"
echo $BUILD_SHORTCOMMIT > "$BUILD_OUTPUT/BUILD_SHORTCOMMIT.env"

dotnet publish src/test-auth.csproj --os linux --arch x64 -c Release -p:ContainerImageTag=$BUILD_NUMBER
if [ $? -ne 0 ]; then
    echo "Build failed."
else
    docker push mindmatrix/test-auth:$BUILD_NUMBER
    kubectl set image deployment/test-auth test-auth=mindmatrix/test-auth:$BUILD_NUMBER
fi
