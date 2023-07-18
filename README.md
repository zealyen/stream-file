# .NET Core 6 File Upload Stream

## 簡介

這是一個透過 api 上傳 hex 檔案，並利用 stream & pipeline 的概念來計算 crc，並且將檔案存到本地

## 環境需求

* dotnet cli (使用 .NET Core 6)，若用 docker 就不需要
* docker，若沒有 docker，請到 https://www.docker.com/products/docker-desktop 下載並安裝

## 執行本地開發環境 (用 docker 啟動 server)

```bash
# 將 repo clone 下來
git clone git@github.com:zealyen/stream-file.git

# 進入專案資料夾
cd stream-file

# 建立 docker image，建立完成後會有一個名為 stream-file-img 的 image
# sh 會進入 image 內的 shell，可以用來檢查 image 內的檔案，離開 shell 請輸入 exit
docker compose -f docker-build.yml run --rm app sh

# 檢查 docker image 是否有建立成功，REPOSITORY 欄位應該要有 stream-file-img
docker image ls 

# run docker container
docker run -d -p 8080:80 stream-file-img

# 檢查 docker container 是否有建立成功，IMAGE 欄位應該要有 stream-file-img，且 port 應該為 0.0.0.0:8080->80/tcp
docker ps -a

# 嘗試用網頁連線，輸入以下 url 應該要有資料回傳：["value1","value2"]
http://localhost:8080/api/UploadFile/ 

# 嘗試上傳任一個 hex 檔案，將目錄下的 postman.import.json 匯入 postman，使用 api，並且修改檔案路徑
api 內的 partitions 欄位為 optional，不帶就計算全部檔案的 crc

# 上傳成功後會回傳 crc 計算結果，以及檔案會儲存在 server 內，透過 docker 指令可以查看到 {datetime}.hex
# sh 會進入 container 內的 shell，檔案放在 upload-files 資料夾內，離開 shell 請輸入 exit
docker exec -t -i {container id} sh

```
