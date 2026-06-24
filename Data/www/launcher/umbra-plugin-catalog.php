<?php

require_once(__DIR__ . "/database.php");

try
{
	$repositoryKey = trim($_GET["repository"] ?? "official");
	if($repositoryKey === "") $repositoryKey = "official";

	$db = launcher_database();
	$tableCheck = $db->query("SHOW TABLES LIKE 'launcher_umbra_plugin_releases'");
	if($tableCheck === false || $tableCheck->num_rows === 0)
	{
		launcher_json(array(
			"repository_name" => "Meteor Umbra Plugins",
			"plugins" => array()
		));
		return;
	}

	$repositoryStatement = $db->prepare(
		"SELECT id, name FROM launcher_umbra_plugin_repositories " .
		"WHERE repository_key = ? AND is_active = 1 LIMIT 1");
	if($repositoryStatement === false)
	{
		launcher_json(array(
			"repository_name" => "Meteor Umbra Plugins",
			"plugins" => array()
		));
		return;
	}

	$repositoryStatement->bind_param("s", $repositoryKey);
	$repositoryStatement->execute();
	$repositoryResult = $repositoryStatement->get_result();
	$repository = $repositoryResult->fetch_assoc();
	if($repository === null)
	{
		launcher_json(array(
			"repository_name" => "Meteor Umbra Plugins",
			"plugins" => array()
		));
		return;
	}

	$repositoryId = intval($repository["id"]);
	$statement = $db->prepare(
		"SELECT plugin_key, name, version, api_version, author, description, " .
		"download_url, size_bytes, sha256, minimum_framework_version, is_active " .
		"FROM launcher_umbra_plugin_releases " .
		"WHERE repository_id = ? AND is_active = 1 " .
		"ORDER BY sort_order ASC, name ASC, plugin_key ASC, version DESC, id ASC");
	if($statement === false)
	{
		launcher_json(array(
			"repository_name" => $repository["name"],
			"plugins" => array()
		));
		return;
	}

	$statement->bind_param("i", $repositoryId);
	$statement->execute();
	$result = $statement->get_result();

	$plugins = array();
	while($row = $result->fetch_assoc())
	{
		$plugins[] = array(
			"id" => $row["plugin_key"],
			"name" => $row["name"],
			"version" => $row["version"],
			"api_version" => $row["api_version"],
			"author" => $row["author"],
			"description" => $row["description"],
			"download_url" => $row["download_url"],
			"size_bytes" => intval($row["size_bytes"]),
			"sha256" => $row["sha256"],
			"minimum_framework_version" => $row["minimum_framework_version"],
			"is_active" => intval($row["is_active"]) === 1
		);
	}

	launcher_json(array(
		"repository_name" => $repository["name"],
		"plugins" => $plugins
	));
}
catch(Exception $e)
{
	launcher_json(array(
		"repository_name" => "Meteor Umbra Plugins",
		"plugins" => array()
	));
}

?>
