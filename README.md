# Overview

# Dependencies

1. Execute the following command to start the dependencies locally:

```bash
docker compose -f docker-compose-dependencies.yaml up -d
```

1. Execute the following command to stop the dependencies locally:

```bash
docker compose -f docker-compose-dependencies.yaml down
```

# Running the code

## Running the code locally

## Running the code in a container

docker build --build-arg FunctionDir=ExternalFunction -t az-func-with-sb-external .
docker build --build-arg FunctionDir=InternalFunction -t az-func-with-sb-internal .
docker run -it -p 8080:80 --name az-func-with-sb-external az-func-with-sb-external:latest

docker rm az-func-with-sb-external