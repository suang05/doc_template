# syntax=docker/dockerfile:1
# =========================================================================
# Stage 1: Dependencies
# =========================================================================
FROM node:20-alpine AS deps
RUN apk add --no-cache libc6-compat
WORKDIR /app

COPY package*.json ./
RUN npm ci

# =========================================================================
# Stage 2: Build Application
# =========================================================================
FROM node:20-alpine AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .

ARG NEXT_PUBLIC_API_URL="http://localhost:8080"
ARG NEXT_PUBLIC_DEFAULT_API_KEY=""
ENV NEXT_PUBLIC_API_URL=${NEXT_PUBLIC_API_URL}
ENV NEXT_PUBLIC_DEFAULT_API_KEY=${NEXT_PUBLIC_DEFAULT_API_KEY}

ENV NODE_OPTIONS="--max-old-space-size=4096"
RUN npm run build

# =========================================================================
# Stage 3: Runner (Production)
# =========================================================================
FROM node:20-alpine AS runner
WORKDIR /app

ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME="0.0.0.0"

RUN addgroup --system --gid 1001 nodejs
RUN adduser --system --uid 1001 nextjs

# Copy public assets
COPY --from=builder /app/public ./public

# Copy standalone build
COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static

USER nextjs

EXPOSE 3000

CMD ["node", "server.js"]
