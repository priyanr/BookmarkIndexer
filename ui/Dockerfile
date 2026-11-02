FROM node:22-alpine AS build
WORKDIR /ui
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# Next writes the /api rewrite target into the build, so the API address is fixed here, not at run time.
ARG API_URL=http://api:5080
ENV API_URL=$API_URL
RUN npm run build

FROM node:22-alpine
WORKDIR /ui
# The standalone server does not include static assets; Next expects them next to server.js.
COPY --from=build /ui/.next/standalone ./
COPY --from=build /ui/.next/static ./.next/static
COPY --from=build /ui/public ./public
ENV PORT=3000 HOSTNAME=0.0.0.0
EXPOSE 3000
CMD ["node", "server.js"]
