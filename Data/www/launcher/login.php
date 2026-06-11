<?php

require_once(__DIR__ . "/auth.php");

try
{
	if($_SERVER["REQUEST_METHOD"] !== "POST")
	{
		http_response_code(405);
		launcher_json(array("success" => false, "message" => "POST required."));
		return;
	}

	$payload = launcher_read_json_or_form();
	$username = launcher_required_string($payload, "username", "Username");
	$password = launcher_required_string($payload, "password", "Password");

	$db = launcher_database();
	$userId = launcher_verify_user($db, $username, $password);
	$sessionId = launcher_refresh_or_create_session($db, $userId);

	launcher_json(array(
		"success" => true,
		"message" => "Login accepted.",
		"username" => $username,
		"session_id" => $sessionId
	));
}
catch(Exception $e)
{
	http_response_code(400);
	launcher_json(array(
		"success" => false,
		"message" => $e->getMessage()
	));
}

?>
