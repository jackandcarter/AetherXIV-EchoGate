-- AetherXIV 2.0 initial schema.
-- Runtime target database: aetherxiv2.
-- This migration is intentionally separate from the current v1 database.

CREATE DATABASE IF NOT EXISTS `aetherxiv2` DEFAULT CHARACTER SET utf8mb4;
USE `aetherxiv2`;

CREATE TABLE IF NOT EXISTS `schema_migrations` (
  `migration_id` varchar(96) NOT NULL,
  `description` varchar(255) NOT NULL,
  `applied_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`migration_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `accounts` (
  `account_id` int unsigned NOT NULL AUTO_INCREMENT,
  `login_name` varchar(64) NOT NULL,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`account_id`),
  UNIQUE KEY `uq_accounts_login_name` (`login_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `account_sessions` (
  `session_token` varchar(128) NOT NULL,
  `account_id` int unsigned NOT NULL,
  `expires_at` timestamp NOT NULL,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`session_token`),
  KEY `idx_account_sessions_account` (`account_id`),
  CONSTRAINT `fk_account_sessions_account` FOREIGN KEY (`account_id`) REFERENCES `accounts` (`account_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `worlds` (
  `world_id` int unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(64) NOT NULL,
  `host` varchar(128) NOT NULL,
  `port` smallint unsigned NOT NULL,
  PRIMARY KEY (`world_id`),
  UNIQUE KEY `uq_worlds_name` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `zones` (
  `zone_id` int unsigned NOT NULL,
  `world_id` int unsigned NOT NULL,
  `name` varchar(96) NOT NULL,
  `region_id` int unsigned NOT NULL DEFAULT 0,
  `is_private` bit NOT NULL DEFAULT 0,
  `load_nav_mesh` bit NOT NULL DEFAULT 0,
  PRIMARY KEY (`zone_id`),
  KEY `idx_zones_world` (`world_id`),
  CONSTRAINT `fk_zones_world` FOREIGN KEY (`world_id`) REFERENCES `worlds` (`world_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `characters` (
  `character_id` int unsigned NOT NULL AUTO_INCREMENT,
  `account_id` int unsigned NOT NULL,
  `world_id` int unsigned NOT NULL,
  `name` varchar(64) NOT NULL,
  `current_zone_id` int unsigned NOT NULL,
  `position_x` float NOT NULL DEFAULT 0,
  `position_y` float NOT NULL DEFAULT 0,
  `position_z` float NOT NULL DEFAULT 0,
  `rotation` float NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`character_id`),
  UNIQUE KEY `uq_characters_world_name` (`world_id`, `name`),
  KEY `idx_characters_account` (`account_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `character_appearance` (
  `character_id` int unsigned NOT NULL,
  `payload_json` json NOT NULL,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`character_id`),
  CONSTRAINT `fk_character_appearance_character` FOREIGN KEY (`character_id`) REFERENCES `characters` (`character_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `provenance_refs` (
  `provenance_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `evidence_status` varchar(32) NOT NULL,
  `source_type` varchar(64) NOT NULL,
  `source_ref` varchar(255) NOT NULL,
  `notes` varchar(255) NOT NULL DEFAULT '',
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`provenance_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `static_actor_spawns` (
  `spawn_id` int unsigned NOT NULL AUTO_INCREMENT,
  `actor_class_id` int unsigned NOT NULL,
  `unique_id` varchar(96) NOT NULL,
  `zone_id` int unsigned NOT NULL,
  `private_area_name` varchar(96) DEFAULT NULL,
  `private_area_level` int unsigned NOT NULL DEFAULT 0,
  `position_x` float NOT NULL,
  `position_y` float NOT NULL,
  `position_z` float NOT NULL,
  `rotation` float NOT NULL,
  `provenance_id` bigint unsigned NOT NULL,
  PRIMARY KEY (`spawn_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `battle_npc_pools` (
  `pool_id` int unsigned NOT NULL,
  `actor_class_id` int unsigned NOT NULL,
  `name` varchar(96) NOT NULL,
  `genus_id` int unsigned NOT NULL,
  PRIMARY KEY (`pool_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `battle_npc_groups` (
  `group_id` int unsigned NOT NULL,
  `pool_id` int unsigned NOT NULL,
  `zone_id` int unsigned NOT NULL,
  `script_name` varchar(96) NOT NULL,
  `min_level` tinyint unsigned NOT NULL,
  `max_level` tinyint unsigned NOT NULL,
  `respawn_seconds` int unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`group_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `battle_npc_spawns` (
  `battle_npc_id` int unsigned NOT NULL,
  `group_id` int unsigned NOT NULL,
  `position_x` float NOT NULL,
  `position_y` float NOT NULL,
  `position_z` float NOT NULL,
  `rotation` float NOT NULL,
  `provenance_id` bigint unsigned NOT NULL,
  PRIMARY KEY (`battle_npc_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `script_modules` (
  `script_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `script_path` varchar(255) NOT NULL,
  `script_role` varchar(64) NOT NULL,
  `content_hash` varchar(128) NOT NULL,
  `provenance_id` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`script_id`),
  UNIQUE KEY `uq_script_modules_path` (`script_path`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `world_state` (
  `state_key` varchar(96) NOT NULL,
  `state_value` varchar(255) NOT NULL,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`state_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT IGNORE INTO `schema_migrations` (`migration_id`, `description`)
VALUES ('20260701_000001_aetherxiv2_initial_schema', 'Initial AetherXIV 2.0 auth/session/character/world/actor/provenance schema.');
