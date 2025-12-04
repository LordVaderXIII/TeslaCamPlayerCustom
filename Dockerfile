FROM mcr.microsoft.com/dotnet/sdk:8.0 AS publish
COPY src /src
WORKDIR "/src/TeslaCamPlayer.BlazorHosted/Server"
RUN dotnet restore .
RUN dotnet publish *.csproj -c Release -o /app/publish /p:DefineConstants=DOCKER
# Remove ffprobe.exe from docker builds. It relies on an apt package instead.
RUN rm -r /app/publish/lib/

FROM node:20-alpine AS gulp
WORKDIR /src
COPY src/TeslaCamPlayer.BlazorHosted/Client/package.json .
COPY src/TeslaCamPlayer.BlazorHosted/Client/gulpfile.js .
COPY src/TeslaCamPlayer.BlazorHosted/Client/wwwroot/scss/ ./wwwroot/scss/
RUN npm install
RUN npm install -g gulp
RUN gulp default

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
RUN apk add --no-cache ffmpeg
WORKDIR /app
ENV ClipsRootPath=/TeslaCam
ENV ASPNETCORE_HTTP_PORTS=80
EXPOSE 80/tcp
COPY --from=publish /app/publish .
COPY --from=gulp /src/wwwroot/css/ ./wwwroot/css/

ENTRYPOINT ["dotnet", "TeslaCamPlayer.BlazorHosted.Server.dll"]
