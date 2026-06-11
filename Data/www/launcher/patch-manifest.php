<?php

require_once(__DIR__ . "/database.php");

try
{
	$db = launcher_database();
	$config = launcher_config_map($db);
	$statement = $db->prepare("SELECT relative_path, size_bytes, crc32, sha256 FROM launcher_patch_files WHERE is_active = 1 ORDER BY sort_order ASC, id ASC");
	$statement->execute();
	$result = $statement->get_result();
	$files = array();
	while($row = $result->fetch_assoc())
	{
		$files[] = array(
			"relative_path" => $row["relative_path"],
			"size_bytes" => intval($row["size_bytes"]),
			"crc32" => strtoupper($row["crc32"]),
			"sha256" => $row["sha256"]
		);
	}
	launcher_json(array(
		"target_boot_version" => $config["target_boot_version"] ?? "2010.09.18.0000",
		"target_game_version" => $config["target_game_version"] ?? "2012.09.19.0001",
		"patch_base_url" => $config["patch_base_url"] ?? "",
		"files" => $files
	));
}
catch(Exception $e)
{
	http_response_code(500);
	launcher_json(array("files" => array()));
}

?>
