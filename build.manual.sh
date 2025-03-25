#!/bin/bash
PRJ=""
IMAGE_TAG="platform.trafficdatacollection.extractframes.service"
VER="dev.1.0.3.14"
RELEASE_FOLDER=""
# wait k8s export image
REGISTRY_URL="vmapi/hubcentral"
findPRJ() {
     for i in *.csproj; do
          PRJ="${i%.*}"
          # IMAGE_TAG="${PRJ,,}"
          break
     done
}
publishNetCore() {
     RELEASE_FOLDER="bin/release/$PRJ"
     rm -Rf $RELEASE_FOLDER
     mkdir -p $RELEASE_FOLDER
     dotnet publish $PRJ.csproj -c release -o ./$RELEASE_FOLDER/app
     rm -f "$RELEASE_FOLDER/Dockerfile"
     echo "
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY /app ./

# Tạo thư mục output và tools
RUN mkdir -p /app/ExtractedFrames /app/tools

# Cài đặt FFmpeg nếu chưa có
RUN apt-get update && apt-get install -y ffmpeg

# Tạo symbolic link - sử dụng đường dẫn trực tiếp thay vì biến
RUN cp -f /usr/bin/ffmpeg /app/tools/ffmpeg && \
    chmod +x /app/tools/ffmpeg

ENTRYPOINT [\"dotnet\", \"$PRJ.dll\"]
         " > "$RELEASE_FOLDER/Dockerfile"
}
buildDocker () {
     docker rmi -f $IMAGE_TAG.$VER
     docker build -f $RELEASE_FOLDER/Dockerfile -t $IMAGE_TAG.$VER $RELEASE_FOLDER/.
     docker tag $IMAGE_TAG.$VER $REGISTRY_URL:$IMAGE_TAG.$VER
     docker push $REGISTRY_URL:$IMAGE_TAG.$VER
}
startBuild() {
     findPRJ
     publishNetCore
     buildDocker
}
startBuild