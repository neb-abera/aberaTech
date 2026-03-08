# aberaTech
Open docker on your machine and run the following commands to build and test the docker image in development
```
docker build -f aberaTech.Server/Dockerfile -t aberatech .
docker run --rm -p 8080:8080 --name aberatech-app aberatech
```