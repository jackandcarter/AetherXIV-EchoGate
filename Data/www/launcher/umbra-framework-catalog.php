<?php

require_once(__DIR__ . "/database.php");

try
{
	$platform = trim($_GET["platform"] ?? "win-x86");
	if($platform === "") $platform = "win-x86";

	$db = launcher_database();
	$tableCheck = $db->query("SHOW TABLES LIKE 'launcher_umbra_framework_artifacts'");
	if($tableCheck === false || $tableCheck->num_rows === 0)
	{
		launcher_json(array(
			"platform" => $platform,
			"artifacts" => array()
		));
		return;
	}

	$statement = $db->prepare(
		"SELECT name, version, api_version, platform_rid, archive_url, archive_format, " .
		"size_bytes, sha256, bootstrap_relative_path, framework_relative_path, " .
		"supported_game_sha256, is_default, is_active, sort_order " .
		"FROM launcher_umbra_framework_artifacts " .
		"WHERE platform_rid = ? AND is_active = 1 " .
		"ORDER BY is_default DESC, sort_order ASC, id ASC");
	if($statement === false)
	{
		launcher_json(array(
			"platform" => $platform,
			"artifacts" => array()
		));
		return;
	}

	$statement->bind_param("s", $platform);
	$statement->execute();
	$result = $statement->get_result();

	$artifacts = array();
	while($row = $result->fetch_assoc())
	{
		$artifacts[] = array(
			"name" => $row["name"],
			"version" => $row["version"],
			"api_version" => $row["api_version"],
			"platform_rid" => $row["platform_rid"],
			"archive_url" => $row["archive_url"],
			"archive_format" => $row["archive_format"],
			"size_bytes" => intval($row["size_bytes"]),
			"sha256" => $row["sha256"],
			"bootstrap_relative_path" => $row["bootstrap_relative_path"],
			"framework_relative_path" => $row["framework_relative_path"],
			"supported_game_sha256" => launcher_config_list($row["supported_game_sha256"] ?? ""),
			"is_default" => intval($row["is_default"]) === 1,
			"is_active" => intval($row["is_active"]) === 1,
			"sort_order" => intval($row["sort_order"])
		);
	}

	launcher_json(array(
		"platform" => $platform,
		"artifacts" => $artifacts
	));
}
catch(Exception $e)
{
	launcher_json(array(
		"platform" => $_GET["platform"] ?? "win-x86",
		"artifacts" => array()
	));
}

?>
