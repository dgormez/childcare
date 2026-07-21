/**
 * publicApiClient.ts — unauthenticated openapi-fetch client for the public enrollment form
 * (feature 023). Distinct from apiClient.ts: no access token, no 401-refresh-and-retry
 * middleware, no Authorization header — the public endpoints carry no JWT `tenant_id` claim at
 * all (research.md R1), so wiring in the authenticated client's refresh logic would be both
 * pointless and misleading about what this surface actually is.
 */
import createClient from "openapi-fetch";
import type { paths } from "./generated/api-types";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export const publicApiClient = createClient<paths>({ baseUrl: API_BASE });
