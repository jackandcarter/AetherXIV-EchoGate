<?php

require_once(__DIR__ . "/database.php");

define("ECHO_GATE_SESSION_LENGTH_HOURS", 24);

function launcher_read_json_or_form()
{
	$raw = file_get_contents("php://input");
	$contentType = $_SERVER["CONTENT_TYPE"] ?? "";
	if(stripos($contentType, "application/json") !== false && $raw !== false && trim($raw) !== "")
	{
		$decoded = json_decode($raw, true);
		if(is_array($decoded)) return $decoded;
	}

	return $_POST;
}

function launcher_required_string($payload, $key, $label)
{
	$value = trim($payload[$key] ?? "");
	if($value === "") throw new Exception($label . " is required.");
	return $value;
}

function launcher_generate_sha224()
{
	return bin2hex(random_bytes(28));
}

function launcher_verify_user($connection, $username, $password)
{
	$statement = $connection->prepare("SELECT id, passhash, salt FROM users WHERE name = ?");
	if(!$statement) throw new Exception("Login failed.");

	try
	{
		$statement->bind_param("s", $username);
		$statement->execute();
		$statement->bind_result($id, $storedPasshash, $salt);
		if(!$statement->fetch()) throw new Exception("Incorrect username or password.");

		$hashedPassword = hash("sha224", $password . $salt);
		if($hashedPassword !== $storedPasshash) throw new Exception("Incorrect username or password.");

		return intval($id);
	}
	finally
	{
		$statement->close();
	}
}

function launcher_get_existing_session($connection, $userId)
{
	$statement = $connection->prepare("SELECT id FROM sessions WHERE userId = ? AND expiration > NOW()");
	if(!$statement) throw new Exception("Session lookup failed.");

	try
	{
		$statement->bind_param("i", $userId);
		$statement->execute();
		$statement->bind_result($sessionId);
		if(!$statement->fetch()) return "";
		return $sessionId;
	}
	finally
	{
		$statement->close();
	}
}

function launcher_refresh_session($connection, $sessionId)
{
	$statement = $connection->prepare("UPDATE sessions SET expiration = NOW() + INTERVAL " . ECHO_GATE_SESSION_LENGTH_HOURS . " HOUR WHERE id = ?");
	if(!$statement) throw new Exception("Session refresh failed.");

	try
	{
		$statement->bind_param("s", $sessionId);
		if(!$statement->execute()) throw new Exception("Session refresh failed.");
	}
	finally
	{
		$statement->close();
	}
}

function launcher_create_session($connection, $userId)
{
	$deleteStatement = $connection->prepare("DELETE FROM sessions WHERE userId = ?");
	if(!$deleteStatement) throw new Exception("Session creation failed.");
	try
	{
		$deleteStatement->bind_param("i", $userId);
		$deleteStatement->execute();
	}
	finally
	{
		$deleteStatement->close();
	}

	$sessionId = launcher_generate_sha224();
	$statement = $connection->prepare("INSERT INTO sessions (id, userid, expiration) VALUES (?, ?, NOW() + INTERVAL " . ECHO_GATE_SESSION_LENGTH_HOURS . " HOUR)");
	if(!$statement) throw new Exception("Session creation failed.");

	try
	{
		$statement->bind_param("si", $sessionId, $userId);
		if(!$statement->execute()) throw new Exception("Session creation failed.");
		return $sessionId;
	}
	finally
	{
		$statement->close();
	}
}

function launcher_refresh_or_create_session($connection, $userId)
{
	$sessionId = launcher_get_existing_session($connection, $userId);
	if($sessionId !== "")
	{
		launcher_refresh_session($connection, $sessionId);
		return $sessionId;
	}

	return launcher_create_session($connection, $userId);
}

function launcher_create_user($connection, $username, $password, $email)
{
	$checkStatement = $connection->prepare("SELECT id FROM users WHERE name = ?");
	if(!$checkStatement) throw new Exception("Account creation failed.");
	try
	{
		$checkStatement->bind_param("s", $username);
		$checkStatement->execute();
		$checkStatement->store_result();
		if($checkStatement->num_rows > 0) throw new Exception("Username is already in use.");
	}
	finally
	{
		$checkStatement->close();
	}

	$salt = launcher_generate_sha224();
	$passhash = hash("sha224", $password . $salt);
	$statement = $connection->prepare("INSERT INTO users (name, passhash, salt, email) VALUES (?, ?, ?, ?)");
	if(!$statement) throw new Exception("Account creation failed.");

	try
	{
		$statement->bind_param("ssss", $username, $passhash, $salt, $email);
		if(!$statement->execute()) throw new Exception("Account creation failed.");
		return intval($connection->insert_id);
	}
	catch(mysqli_sql_exception $e)
	{
		if(intval($e->getCode()) === 1062) throw new Exception("Username is already in use.");
		throw $e;
	}
	finally
	{
		$statement->close();
	}
}

?>
