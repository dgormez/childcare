variable "subscription_id" {
  type        = string
  description = "Azure subscription ID"
}

variable "resource_group_name" {
  type    = string
  default = "childcare-rg"
}

variable "location" {
  type        = string
  default     = "westeurope"
  description = "Azure region (e.g. westeurope, eastus)"
}

variable "acr_name" {
  type        = string
  default     = "childcareregistry"
  description = "Azure Container Registry name — globally unique, alphanumeric only, no hyphens"
}

variable "app_name" {
  type    = string
  default = "childcare-api"
}

variable "image" {
  type        = string
  default     = "mcr.microsoft.com/dotnet/aspnet:10.0"
  description = "Initial container image — use the default placeholder for first apply, GitHub Actions replaces it on deploy"
}

variable "github_username" {
  type        = string
  description = "GitHub username or org — used to scope the federated identity credential"
}

variable "github_repo" {
  type        = string
  description = "GitHub repo name — used to scope the federated identity credential"
}

variable "db_connection_string" {
  type      = string
  sensitive = true
}

variable "jwt_secret" {
  type      = string
  sensitive = true
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
