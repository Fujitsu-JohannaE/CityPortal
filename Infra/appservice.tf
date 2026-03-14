# ─── App Service Plan + Web App (.NET 8) ─────────────────────────────────────

resource "azurerm_service_plan" "main" {
  name                = "plan-${var.project}-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.app_service_sku

  tags = local.tags
}

resource "azurerm_linux_web_app" "main" {
  name                = "app-${var.project}-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.main.id

  # System-assigned Managed Identity — used for SQL + Storage auth
  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      dotnet_version = "8.0"
    }

    always_on = var.app_service_sku != "F1" # Free tier doesn't support always_on
  }

  # ── App settings (injected as environment variables) ──────────────────────
  app_settings = {
    # Storage — Managed Identity auth (no connection string)
    "AzureStorage__ServiceUri" = azurerm_storage_account.attachments.primary_blob_endpoint

    # EF Core runs migrations at startup, needs this
    "ASPNETCORE_ENVIRONMENT" = var.environment == "prod" ? "Production" : "Development"
  }

  # ── Connection string for SQL ─────────────────────────────────────────────
  connection_string {
    name  = "DefaultConnection"
    type  = "SQLAzure"
    value = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.main.name};User ID=${var.sql_admin_login};Password=${var.sql_admin_password};Encrypt=True;TrustServerCertificate=False;"
  }

  tags = local.tags
}
