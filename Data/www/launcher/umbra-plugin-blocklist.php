<?php

require_once(__DIR__ . "/database.php");

try
{
	$db = launcher_database();
	if(!launcher_table_exists($db, "launcher_umbra_plugin_blocks"))
	{
		launcher_json(array("blocks" => array()));
		return;
	}

	$statement = $db->prepare(
		"SELECT plugin_key, repository_url, version, reason FROM launcher_umbra_plugin_blocks " .
		"WHERE is_active = 1 ORDER BY plugin_key ASC, version ASC, id ASC");
	if($statement === false)
	{
		launcher_json(array("blocks" => array()));
		return;
	}

	$statement->execute();
	$result = $statement->get_result();

	$blocks = array();
	while($row = $result->fetch_assoc())
	{
		$blocks[] = array(
			"plugin_id" => $row["plugin_key"],
			"repository_url" => $row["repository_url"],
			"version" => $row["version"],
			"reason" => $row["reason"]
		);
	}

	launcher_json(array("blocks" => $blocks));
}
catch(Exception $e)
{
	launcher_json(array("blocks" => array()));
}

?>
