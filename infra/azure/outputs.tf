output "app_url" {
  description = "Public URL of the Container App"
  value       = "https://${azurerm_container_app.api.latest_revision_fqdn}"
}

output "acr_login_server" {
  description = "ACR login server — use as ACR_LOGIN_SERVER GitHub secret"
  value       = azurerm_container_registry.acr.login_server
}

output "azure_client_id" {
  description = "Managed identity client ID — use as AZURE_CLIENT_ID GitHub secret"
  value       = azurerm_user_assigned_identity.github_actions.client_id
}

output "azure_tenant_id" {
  description = "Azure tenant ID — use as AZURE_TENANT_ID GitHub secret"
  value       = data.azurerm_client_config.current.tenant_id
}
