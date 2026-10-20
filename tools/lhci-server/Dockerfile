# The Lighthouse CI server image (docs/lighthouse.md). Built here rather than
# pulled as patrickhulce/lhci-server, which runs as root on Node 18.
#
# Dependabot bumps the FROM lines (docker, /tools/lhci-server) and the
# packages (npm, /tools/lhci-server). The Lighthouse job builds this image on
# every pull request. The lighthouse-server workflow pushes it and rolls the
# container app on a merge that changes it.
FROM node:26-slim@sha256:193fe51b64e77981119c98c2002c9e32a70e2f006fb4d25068ce0558998917f0 AS lhci-server-deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --omit=dev --no-audit --no-fund

FROM node:26-slim@sha256:193fe51b64e77981119c98c2002c9e32a70e2f006fb4d25068ce0558998917f0 AS lhci-server
ENV NODE_ENV=production
WORKDIR /app
COPY --from=lhci-server-deps --chown=node:node /app/node_modules ./node_modules
COPY --chown=node:node package.json server.mjs gate.mjs ./
USER node
EXPOSE 9001
CMD ["node", "server.mjs"]
