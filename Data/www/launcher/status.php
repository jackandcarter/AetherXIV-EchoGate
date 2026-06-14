<?php

require_once(__DIR__ . "/database.php");

try
{
	$db = launcher_database();
	$config = launcher_config_map($db);
	launcher_json(array(
		"state" => $config["server_state"] ?? "offline",
		"message" => $config["server_message"] ?? "Status is not configured.",
		"checked_at" => gmdate("c")
	));
}
catch(Exception $e)
{
	http_response_code(500);
	launcher_json(array("state" => "error", "message" => "Status unavailable.", "checked_at" => gmdate("c")));
}

?>
