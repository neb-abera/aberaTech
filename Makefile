# Entry points for working on this repository. Every target runs inside a
# container: none of them needs node, npm or dotnet installed on the machine,
# only Docker.
#
# Run `make` on its own to list them.

COMPOSE    ?= docker compose
DOCKER     ?= docker
DOCKERFILE := aberaTech.Server/Dockerfile

.DEFAULT_GOAL := help
.PHONY: help dev test test-watch lint fmt check image run clean

help: ## List the available targets
	@grep -hE '^[a-z-]+:.*?## ' $(MAKEFILE_LIST) \
		| awk -F':.*?## ' '{printf "  \033[36m%-11s\033[0m %s\n", $$1, $$2}'

dev: ## Hot reloading dev server on http://localhost:3000
	$(COMPOSE) up --build dev

test: ## Unit tests, against the working tree
	$(COMPOSE) build test
	$(COMPOSE) run --rm test

test-watch: ## Unit tests, re-run on every change
	$(COMPOSE) build test
	$(COMPOSE) run --rm test npm run test:watch

lint: ## eslint and prettier, against the working tree
	$(COMPOSE) build lint
	$(COMPOSE) run --rm lint

fmt: ## Rewrite files to match prettier
	$(COMPOSE) build lint
	$(COMPOSE) run --rm lint npx prettier --write .

check: ## The gate CI runs: type check, unit tests, lint and format
	$(DOCKER) build --target clienttest -f $(DOCKERFILE) .
	$(DOCKER) build --target clientlint -f $(DOCKERFILE) .

image: ## Build the production image the deploy pipeline builds
	$(DOCKER) build --build-arg IN_DOCKER=true -t abera-tech -f $(DOCKERFILE) .

run: image ## Build and run the production image on http://localhost:8080
	$(DOCKER) run --rm -p 8080:8080 abera-tech

clean: ## Remove the compose containers and their volumes
	$(COMPOSE) down --volumes --remove-orphans
