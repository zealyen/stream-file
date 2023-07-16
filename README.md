# .NET Core 6 File Upload Stream

## 簡介

這是一個上傳 hex 檔案，透過 stream & pipe 的概念來計算 crc，並且將檔案存到本地

## requirement

* dotnet cli
* docker (為了避免安裝 .NET Core 6 環境)
* 若沒有 docker，請到 https://www.docker.com/products/docker-desktop 下載並安裝

## 執行本地開發環境

```bash
# 將 repo clone 下來
git clone git@github.com:zealyen/stream-file.git

# 進入專案資料夾
cd stream-file

# run docker
docker build -t stream-file-img .

# 檢查 docker image 是否有建立成功，REPOSITORY 欄位應該要有 stream-file-img
docker image ls 

# run docker container
docker run -d -p 8080:80 stream-file-img

# 檢查 docker container 是否有建立成功，IMAGE 欄位應該要有 stream-file-img，且 port 應該為 0.0.0.0:8080->80/tcp
docker ps -a

# 嘗試用網頁連線應該要有資料回傳
http://localhost:8080/api/WeatherForecast/ 

# 嘗試上傳檔案，會再額外提供檔案、postman import json
帶入的參數 partitions 分為 start, end，可以帶入預設值 startAddress: 0(0x0), endAddress: 4294967295(0xFFFFFFFF)，或是自行帶入

# 上傳成功後會回傳 crc 計算結果，以及檔案會儲存在 server 內，透過 docker 指令可以查看到 test.hex
docker exec -t -i {container id} /bin/bash

```
