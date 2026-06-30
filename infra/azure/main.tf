terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
  }
  subscription_id = var.subscription_id
}

data "azurerm_client_config" "current" {}

resource "azurerm_resource_group" "rg" {
  name     = var.resource_group_name
  location = var.location
}

# GitHub Actions identity

resource "azurerm_user_assigned_identity" "github_actions" {
  name                = "childcare-github-actions"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
}

resource "azurerm_role_assignment" "github_actions_contributor" {
  scope                = azurerm_resource_group.rg.id
  role_definition_name = "Contributor"
  principal_id         = azurerm_user_assigned_identity.github_actions.principal_id
}

resource "azurerm_role_assignment" "github_actions_acr_push" {
  scope                = azurerm_container_registry.acr.id
  role_definition_name = "AcrPush"
  principal_id         = azurerm_user_assigned_identity.github_actions.principal_id
}

resource "azurerm_role_assignment" "github_actions_acr_pull" {
  scope                = azurerm_container_registry.acr.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.github_actions.principal_id
}

resource "azurerm_federated_identity_credential" "github_actions" {
  name                = "github-actions-master"
  resource_group_name = azurerm_resource_group.rg.name
  parent_id           = azurerm_user_assigned_identity.github_actions.id
  audience            = ["api://AzureADTokenExchange"]
  issuer              = "https://token.actions.githubusercontent.com"
  subject             = "repo:${var.github_username}/${var.github_repo}:ref:refs/heads/master"
}

# Container Registry

resource "azurerm_container_registry" "acr" {
  name                = var.acr_name
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  sku                 = "Basic"
}

# Container Apps

resource "azurerm_container_app_environment" "env" {
  name                = "${var.app_name}-env"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name

}

resource "azurerm_container_app" "api" {
  name                         = var.app_name
  container_app_environment_id = azurerm_container_app_environment.env.id
  resource_group_name          = azurerm_resource_group.rg.name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.github_actions.id]
  }

  registry {
    server   = azurerm_container_registry.acr.login_server
    identity = azurerm_user_assigned_identity.github_actions.id
  }

  template {
    min_replicas = 0
    max_replicas = 3

    container {
      name   = "api"
      image  = var.image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }
      env {
        name        = "ConnectionStrings__DefaultConnection"
        secret_name = "db-connection"
      }
      env {
        name        = "Jwt__Secret"
        secret_name = "jwt-secret"
      }
      env {
        name  = "Jwt__Issuer"
        value = "ChildCare"
      }
      env {
        name  = "Jwt__Audience"
        value = "ChildCareApp"
      }
      env {
        name  = "Jwt__AccessTokenExpiryMinutes"
        value = "15"
      }
      env {
        name  = "Jwt__RefreshTokenExpiryDays"
        value = "30"
      }
      env {
        name  = "Google__AllowedClientIds__0"
        value = var.google_android_client_id
      }
      env {
        name  = "Google__AllowedClientIds__1"
        value = var.google_ios_client_id
      }
      env {
        name  = "Google__AllowedClientIds__2"
        value = var.google_web_client_id
      }
      env {
        name  = "Apple__BundleId"
        value = var.apple_bundle_id
      }
      env {
        name        = "Stripe__SecretKey"
        secret_name = "stripe-secret-key"
      }
      env {
        name        = "Stripe__WebhookSecret"
        secret_name = "stripe-webhook-secret"
      }
      env {
        name  = "Stripe__PriceId"
        value = var.stripe_price_id
      }
      env {
        name  = "Stripe__SuccessUrl"
        value = "childcare://payment-success"
      }
      env {
        name  = "Stripe__CancelUrl"
        value = "childcare://payment-cancel"
      }
    }
  }

  secret {
    name  = "db-connection"
    value = var.db_connection_string
  }

  secret {
    name  = "jwt-secret"
    value = var.jwt_secret
  }

  secret {
    name  = "stripe-secret-key"
    value = var.stripe_secret_key
  }

  secret {
    name  = "stripe-webhook-secret"
    value = var.stripe_webhook_secret
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}
