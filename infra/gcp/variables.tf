variable "project_id" {
  type        = string
  description = "GCP project ID"
}

variable "region" {
  type        = string
  default     = "us-central1"
  description = "GCP region for all resources"
}

variable "service_name" {
  type    = string
  default = "childcare-api"
}

variable "image" {
  type        = string
  default     = "us-docker.pkg.dev/cloudrun/container/hello:latest"
  description = "Initial container image — use the default placeholder for first apply, GitHub Actions replaces it on deploy"
}

variable "github_username" {
  type        = string
  description = "GitHub username or org — used to scope the Workload Identity Federation"
}

variable "github_repo" {
  type        = string
  description = "GitHub repo name — used to scope the Workload Identity Federation"
}

variable "db_connection_string" {
  type      = string
  sensitive = true
}

variable "jwt_secret" {
  type      = string
  sensitive = true
}

variable "jwt_issuer" {
  type    = string
  default = "ChildCare"
}

variable "jwt_audience" {
  type    = string
  default = "ChildCareApp"
}

variable "google_android_client_id" {
  type = string
}

variable "google_ios_client_id" {
  type = string
}

variable "google_web_client_id" {
  type = string
}

variable "apple_bundle_id" {
  type = string
}

variable "stripe_secret_key" {
  type      = string
  sensitive = true
}

variable "stripe_webhook_secret" {
  type      = string
  sensitive = true
}

variable "stripe_price_id" {
  type = string
}

variable "superadmin_api_key" {
  type        = string
  sensitive   = true
  description = "Gates POST /api/admin/invitations (research.md R11) — a temporary Phase 1 measure until proper super-admin auth exists (Phase 2)."
}
