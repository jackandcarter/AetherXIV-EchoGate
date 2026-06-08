<?php

$db_server		= "localhost";
$db_port		= 3306;
$db_username	= "root";
$db_password	= "";
$db_database	= "ffxiv_server";

$recaptcha_publickey = "";
$recaptcha_privatekey = "";

if(!defined('FFXIV_SESSION_LENGTH')) define('FFXIV_SESSION_LENGTH', 24);		//Session length in hours

$db_server_env = getenv("METEOR_DB_HOST");
$db_port_env = getenv("METEOR_DB_PORT");
$db_username_env = getenv("METEOR_DB_USER");
$db_password_env = getenv("METEOR_DB_PASS");
$db_database_env = getenv("METEOR_DB_NAME");

if($db_server_env !== false && $db_server_env !== "") $db_server = $db_server_env;
if($db_port_env !== false && $db_port_env !== "") $db_port = intval($db_port_env);
if($db_username_env !== false && $db_username_env !== "") $db_username = $db_username_env;
if($db_password_env !== false) $db_password = $db_password_env;
if($db_database_env !== false && $db_database_env !== "") $db_database = $db_database_env;

$local_config = __DIR__ . "/config.local.php";
if(file_exists($local_config)) require($local_config);

?>
