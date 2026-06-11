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
	$confirmPassword = launcher_required_string($payload, "confirm_password", "Confirm password");
	$email = launcher_required_string($payload, "email", "Email");

	if($password !== $confirmPassword) throw new Exception("Passwords do not match.");
	if(!filter_var($email, FILTER_VALIDATE_EMAIL)) throw new Exception("A valid email address is required.");

	$db = launcher_database();
	$userId = launcher_create_user($db, $username, $password, $email);
	$sessionId = launcher_refresh_or_create_session($db, $userId);

	launcher_json(array(
		"success" => true,
		"message" => "Account created.",
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
