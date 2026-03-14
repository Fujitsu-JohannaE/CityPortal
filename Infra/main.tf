terraform {
  required_version = ">= 1.5"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}

provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
}

# ─── Data: current Azure CLI / SP identity ────────────────────────────────────

data "azurerm_client_config" "current" {}

# ─── Resource Group ───────────────────────────────────────────────────────────

resource "azurerm_resource_group" "main" {
  name     = "rg-${var.project}-${var.environment}"
  location = var.location

  tags = local.tags
}

# ─── Locals ───────────────────────────────────────────────────────────────────

locals {
  tags = {
    project     = var.project
    environment = var.environment
    managed_by  = "terraform"
  }
}
