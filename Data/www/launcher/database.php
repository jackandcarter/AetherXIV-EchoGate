<?php

require_once(__DIR__ . "/config.php");

mysqli_report(MYSQLI_REPORT_STRICT);

function launcher_database()
{
	global $db_server, $db_port, $db_username, $db_password, $db_database;
	$connection = new mysqli($db_server, $db_username, $db_password, $db_database, $db_port);
	$connection->query("SET NAMES 'utf8'");
	return $connection;
}

function launcher_config_map($connection)
{
	$result = $connection->query("SELECT config_key, config_value FROM launcher_config");
	$config = array();
	while($row = $result->fetch_assoc())
	{
		$config[$row["config_key"]] = $row["config_value"];
	}
	return $config;
}

function launcher_json($payload)
{
	header("Content-Type: application/json; charset=utf-8");
	header("Cache-Control: no-store");
	echo json_encode($payload, JSON_UNESCAPED_SLASHES);
}

?>
