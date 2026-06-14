<?php

require_once(__DIR__ . "/database.php");

try
{
	$platform = $_GET["platform"] ?? "";
	$db = launcher_database();
	$statement = $db->prepare(
		"SELECT name, version, platform_rid, runtime_kind, archive_url, archive_format, size_bytes, sha256, executable_relative_path, prefix_arch, environment_json, is_default, is_active, sort_order " .
		"FROM launcher_runtime_artifacts " .
		"WHERE is_active = 1 AND (? = '' OR platform_rid = ?) " .
		"ORDER BY is_default DESC, sort_order ASC, id ASC");
	$statement->bind_param("ss", $platform, $platform);
	$statement->execute();
	$result = $statement->get_result();
	$artifacts = array();
	while($row = $result->fetch_assoc())
	{
		$environment = array();
		if(!empty($row["environment_json"]))
		{
			$decoded = json_decode($row["environment_json"], true);
			if(is_array($decoded)) $environment = $decoded;
		}

		$artifacts[] = array(
			"name" => $row["name"],
			"version" => $row["version"],
			"platform_rid" => $row["platform_rid"],
			"runtime_kind" => $row["runtime_kind"],
			"archive_url" => $row["archive_url"],
			"archive_format" => $row["archive_format"],
			"size_bytes" => intval($row["size_bytes"]),
			"sha256" => strtoupper($row["sha256"]),
			"executable_relative_path" => $row["executable_relative_path"],
			"prefix_arch" => $row["prefix_arch"],
			"environment" => $environment,
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
	launcher_json(array("platform" => $_GET["platform"] ?? "", "artifacts" => array()));
}

?>
