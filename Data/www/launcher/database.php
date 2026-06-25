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

function launcher_config_list($value)
{
	$items = array();
	foreach(preg_split("/[\r\n;]+/", $value ?? "") as $item)
	{
		$item = trim($item);
		if($item !== "") $items[] = $item;
	}
	return array_values(array_unique($items));
}

function launcher_table_exists($connection, $tableName)
{
	$statement = $connection->prepare("SHOW TABLES LIKE ?");
	if($statement === false) return false;
	$statement->bind_param("s", $tableName);
	$statement->execute();
	$result = $statement->get_result();
	return $result !== false && $result->num_rows > 0;
}

function launcher_column_exists($connection, $tableName, $columnName)
{
	if(!preg_match("/^[A-Za-z0-9_]+$/", $tableName)) return false;
	$statement = $connection->prepare("SHOW COLUMNS FROM `$tableName` LIKE ?");
	if($statement === false) return false;
	$statement->bind_param("s", $columnName);
	$statement->execute();
	$result = $statement->get_result();
	return $result !== false && $result->num_rows > 0;
}

function launcher_umbra_supported_repository_urls($connection)
{
	if(!launcher_table_exists($connection, "launcher_umbra_plugin_repositories"))
		return array();
	if(!launcher_column_exists($connection, "launcher_umbra_plugin_repositories", "repository_url"))
		return array();

	$result = $connection->query(
		"SELECT repository_url FROM launcher_umbra_plugin_repositories " .
		"WHERE is_supported = 1 AND is_active = 1 AND repository_url <> '' " .
		"ORDER BY sort_order ASC, name ASC, id ASC");
	if($result === false) return array();

	$urls = array();
	while($row = $result->fetch_assoc())
	{
		$url = trim($row["repository_url"] ?? "");
		if($url !== "") $urls[] = $url;
	}

	return array_values(array_unique($urls));
}

?>
