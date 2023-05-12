#!/usr/bin/env bash

SCRIPT="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

docker network inspect rabbitmq_net >/dev/null 2>&1 || docker network create rabbitmq_net
docker rm -f keycloak 2>/dev/null || echo "keycloak was not running"

echo "Running keycloack docker image ..."

SIGNING_KEY_FILE=$SCRIPT/keycloak/signing-key/signing-key.pem
if [ -f "$SIGNING_KEY_FILE" ]; then
    EXTRA_MOUNTS="${EXTRA_MOUNTS} -v ${SIGNING_KEY_FILE}:/etc/rabbitmq/signing-key.pem"
fi

docker run \
		--detach \
		--name keycloak --net rabbitmq_net \
		--publish 8080:8080 \
		--env KEYCLOAK_ADMIN=admin \
		--env KEYCLOAK_ADMIN_PASSWORD=admin \
		--mount type=bind,source=${SCRIPT}/keycloak/import/,target=/opt/keycloak/data/import/ \
		${EXTRA_MOUNTS} \
		quay.io/keycloak/keycloak:20.0 start-dev --import-realm
