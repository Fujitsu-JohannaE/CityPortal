# ─── Outputs ──────────────────────────────────────────────────────────────────

output "resource_group_name" {
  value = azurerm_resource_group.main.name
}

output "app_service_url" {
  value = "https://${azurerm_linux_web_app.main.default_hostname}"
}

output "app_service_name" {
  value = azurerm_linux_web_app.main.name
}

output "sql_server_fqdn" {
  value = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "sql_database_name" {
  value = azurerm_mssql_database.main.name
}

output "storage_account_name" {
  value = azurerm_storage_account.attachments.name
}

output "storage_blob_endpoint" {
  value = azurerm_storage_account.attachments.primary_blob_endpoint
}

output "managed_identity_principal_id" {
  value = azurerm_linux_web_app.main.identity[0].principal_id
}
