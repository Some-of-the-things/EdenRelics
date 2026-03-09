# Stage 1: Build
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# Stage 2: Production
FROM node:22-alpine
WORKDIR /app

COPY --from=build /app/dist/eden-relics ./dist/eden-relics

ENV PORT=4000
EXPOSE 4000

CMD ["node", "dist/eden-relics/server/server.mjs"]
