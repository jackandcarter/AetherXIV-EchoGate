<?php

$path = trim(parse_url($_SERVER["REQUEST_URI"], PHP_URL_PATH), "/");
$parts = explode("/", $path);
$route = end($parts);

if($route === "" || $route === "launcher") $route = "config";

switch($route)
{
	case "config":
		require(__DIR__ . "/config-endpoint.php");
		break;
	case "status":
		require(__DIR__ . "/status.php");
		break;
	case "news":
		require(__DIR__ . "/news.php");
		break;
	case "patch-manifest":
		require(__DIR__ . "/patch-manifest.php");
		break;
	case "runtime-catalog":
		require(__DIR__ . "/runtime-catalog.php");
		break;
	case "framework-catalog":
		require(__DIR__ . "/umbra-framework-catalog.php");
		break;
	case "plugin-catalog":
		require(__DIR__ . "/umbra-plugin-catalog.php");
		break;
	case "login":
		require(__DIR__ . "/login.php");
		break;
	case "create-account":
		require(__DIR__ . "/create-account.php");
		break;
	default:
		http_response_code(404);
		header("Content-Type: application/json; charset=utf-8");
		echo "{\"error\":\"not found\"}";
		break;
}

?>
