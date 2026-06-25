<?php

$db_server = "localhost";
$db_port = 3306;
$db_username = "aetherxiv";
$db_password = "aether_dev";
$db_database = "ffxiv_server";

$db_server_env = getenv("AETHER_DB_HOST");
$db_port_env = getenv("AETHER_DB_PORT");
$db_username_env = getenv("AETHER_DB_USER");
$db_password_env = getenv("AETHER_DB_PASS");
$db_database_env = getenv("AETHER_DB_NAME");
if($db_server_env === false || $db_server_env === "") $db_server_env = getenv("METEOR_DB_HOST");
if($db_port_env === false || $db_port_env === "") $db_port_env = getenv("METEOR_DB_PORT");
if($db_username_env === false || $db_username_env === "") $db_username_env = getenv("METEOR_DB_USER");
if($db_password_env === false) $db_password_env = getenv("METEOR_DB_PASS");
if($db_database_env === false || $db_database_env === "") $db_database_env = getenv("METEOR_DB_NAME");
$launcher_admin_password = "";
$launcher_admin_password_hash = getenv("AETHER_LAUNCHER_ADMIN_PASSWORD_HASH");
$launcher_admin_password_env = getenv("AETHER_LAUNCHER_ADMIN_PASSWORD");
if($launcher_admin_password_hash === false) $launcher_admin_password_hash = getenv("METEOR_LAUNCHER_ADMIN_PASSWORD_HASH");
if($launcher_admin_password_env === false) $launcher_admin_password_env = getenv("METEOR_LAUNCHER_ADMIN_PASSWORD");

if($db_server_env !== false && $db_server_env !== "") $db_server = $db_server_env;
if($db_port_env !== false && $db_port_env !== "") $db_port = intval($db_port_env);
if($db_username_env !== false && $db_username_env !== "") $db_username = $db_username_env;
if($db_password_env !== false) $db_password = $db_password_env;
if($db_database_env !== false && $db_database_env !== "") $db_database = $db_database_env;
if($launcher_admin_password_hash === false) $launcher_admin_password_hash = "";
if($launcher_admin_password_env !== false) $launcher_admin_password = $launcher_admin_password_env;

$local_config = __DIR__ . "/config.local.php";
if(file_exists($local_config)) require($local_config);

?>
