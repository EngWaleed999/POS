-- =============================================================================
-- SuperMarket POS - Database Initialization Script
-- Executed automatically on first container startup by PostgreSQL entrypoint.
-- =============================================================================

-- 1. Keycloak Identity Provider Database
CREATE DATABASE keycloak_db;

-- 2. POS Microservices Databases
CREATE DATABASE sales_db;
CREATE DATABASE inventory_db;
CREATE DATABASE identity_db;
CREATE DATABASE operations_db;
