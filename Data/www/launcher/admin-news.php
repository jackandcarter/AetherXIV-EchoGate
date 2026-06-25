<?php

require_once(__DIR__ . "/database.php");

session_name("meteor_launcher_admin");
session_start();

function launcher_admin_is_local_request()
{
	$remoteAddress = $_SERVER["REMOTE_ADDR"] ?? "";
	return $remoteAddress === "127.0.0.1" || $remoteAddress === "::1" || $remoteAddress === "localhost";
}

function launcher_admin_has_password()
{
	global $launcher_admin_password, $launcher_admin_password_hash;
	return trim($launcher_admin_password ?? "") !== "" || trim($launcher_admin_password_hash ?? "") !== "";
}

function launcher_admin_verify_password($password)
{
	global $launcher_admin_password, $launcher_admin_password_hash;
	if(trim($launcher_admin_password_hash ?? "") !== "")
		return password_verify($password, $launcher_admin_password_hash);

	return hash_equals($launcher_admin_password ?? "", $password);
}

function launcher_admin_escape($value)
{
	return htmlspecialchars($value ?? "", ENT_QUOTES, "UTF-8");
}

function launcher_admin_datetime_value($value)
{
	$timestamp = strtotime($value ?? "");
	if($timestamp === false) $timestamp = time();
	return gmdate("Y-m-d\TH:i", $timestamp);
}

function launcher_admin_datetime_sql($value)
{
	$timestamp = strtotime($value ?? "");
	if($timestamp === false) $timestamp = time();
	return gmdate("Y-m-d H:i:s", $timestamp);
}

function launcher_admin_ensure_news_table($connection)
{
	$connection->query(
		"CREATE TABLE IF NOT EXISTS launcher_news (" .
		"id int(11) unsigned NOT NULL AUTO_INCREMENT, " .
		"title varchar(160) NOT NULL, " .
		"summary varchar(500) NOT NULL, " .
		"body text NULL, " .
		"banner_url varchar(500) NULL, " .
		"link_url varchar(500) NULL, " .
		"published_at datetime NOT NULL DEFAULT CURRENT_TIMESTAMP, " .
		"is_published tinyint(1) NOT NULL DEFAULT 1, " .
		"sort_order int(11) NOT NULL DEFAULT 0, " .
		"PRIMARY KEY (id), " .
		"KEY idx_launcher_news_published (is_published, published_at, sort_order)" .
		") ENGINE=InnoDB DEFAULT CHARSET=utf8");
}

function launcher_admin_render_header($title)
{
	header("Content-Type: text/html; charset=utf-8");
	echo "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">";
	echo "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">";
	echo "<title>" . launcher_admin_escape($title) . "</title>";
	echo "<style>";
	echo "body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;margin:0;background:#f5f7f8;color:#17202a;}";
	echo "main{max-width:1120px;margin:0 auto;padding:28px 18px 48px;}";
	echo "h1{font-size:28px;margin:0 0 6px;} h2{font-size:18px;margin:28px 0 12px;}";
	echo "p{color:#4c5a66;} a{color:#195f9b;} .bar{display:flex;justify-content:space-between;gap:16px;align-items:center;margin-bottom:20px;}";
	echo ".notice{padding:12px 14px;border:1px solid #b9d6bc;background:#eff9f0;border-radius:6px;margin:14px 0;}";
	echo ".error{padding:12px 14px;border:1px solid #e3a7a7;background:#fff1f1;border-radius:6px;margin:14px 0;}";
	echo "form.panel,.panel{background:#fff;border:1px solid #d9e1e7;border-radius:6px;padding:16px;margin:14px 0;}";
	echo "label{display:block;font-weight:600;font-size:13px;margin:10px 0 5px;} input,textarea{box-sizing:border-box;width:100%;padding:9px;border:1px solid #b8c3cc;border-radius:4px;font:inherit;background:#fff;}";
	echo "textarea{min-height:96px;resize:vertical;} .grid{display:grid;grid-template-columns:1fr 1fr;gap:12px;} .checks{display:flex;gap:16px;align-items:center;flex-wrap:wrap;margin:10px 0;}";
	echo ".checks label{display:flex;gap:7px;align-items:center;margin:0;font-weight:500;} .checks input{width:auto;}";
	echo "button,.button{display:inline-flex;align-items:center;justify-content:center;border:1px solid #244c6f;background:#255f8f;color:#fff;padding:8px 12px;border-radius:4px;text-decoration:none;font-weight:600;cursor:pointer;}";
	echo "button.secondary,.button.secondary{background:#fff;color:#244c6f;} button.danger{background:#a83232;border-color:#8b2424;}";
	echo "table{width:100%;border-collapse:collapse;background:#fff;border:1px solid #d9e1e7;} th,td{text-align:left;border-bottom:1px solid #e6edf2;padding:9px;vertical-align:top;} th{font-size:12px;color:#52616d;background:#f0f4f7;} .actions{display:flex;gap:8px;flex-wrap:wrap;} .muted{color:#687782;font-size:13px;} @media(max-width:720px){.grid,.bar{display:block;} th:nth-child(3),td:nth-child(3){display:none;}}";
	echo "</style></head><body><main>";
}

function launcher_admin_render_footer()
{
	echo "</main></body></html>";
}

function launcher_admin_current_path()
{
	$path = parse_url($_SERVER["REQUEST_URI"] ?? "", PHP_URL_PATH);
	return $path === null || $path === "" ? "admin-news" : $path;
}

function launcher_admin_public_news_path($adminPath)
{
	if(strpos($adminPath, "/admin/news") !== false)
		return preg_replace("#/admin/news/?$#", "/news", $adminPath);
	if(strpos($adminPath, "admin-news") !== false)
		return preg_replace("#admin-news/?$#", "news", $adminPath);
	return "news";
}

$isLocal = launcher_admin_is_local_request();
$hasPassword = launcher_admin_has_password();
$isAuthenticated = !$hasPassword && $isLocal;
if($hasPassword && !empty($_SESSION["launcher_admin_authenticated"])) $isAuthenticated = true;

if(!$isLocal && !$hasPassword)
{
	http_response_code(403);
	launcher_admin_render_header("Launcher News Admin");
	echo "<h1>Launcher News Admin</h1><div class=\"error\">Remote admin access is disabled. Set a launcher admin password in config.local.php or AETHER_LAUNCHER_ADMIN_PASSWORD.</div>";
	launcher_admin_render_footer();
	return;
}

$message = "";
$error = "";

if($hasPassword && isset($_POST["admin_login"]))
{
	if(launcher_admin_verify_password($_POST["admin_password"] ?? ""))
	{
		$_SESSION["launcher_admin_authenticated"] = true;
		$isAuthenticated = true;
		$message = "Signed in.";
	}
	else
	{
		$error = "Incorrect admin password.";
	}
}

if(isset($_POST["admin_logout"]))
{
	unset($_SESSION["launcher_admin_authenticated"]);
	$isAuthenticated = !$hasPassword && $isLocal;
	$message = "Signed out.";
}

launcher_admin_render_header("Launcher News Admin");

$adminPath = launcher_admin_current_path();
$publicNewsPath = launcher_admin_public_news_path($adminPath);

echo "<div class=\"bar\"><div><h1>Launcher News Admin</h1><p>Edit the news posts served by the launcher news endpoint.</p></div>";
echo "<a class=\"button secondary\" href=\"" . launcher_admin_escape($publicNewsPath) . "\">Public News JSON</a></div>";

if($message !== "") echo "<div class=\"notice\">" . launcher_admin_escape($message) . "</div>";
if($error !== "") echo "<div class=\"error\">" . launcher_admin_escape($error) . "</div>";

if(!$isAuthenticated)
{
	echo "<form class=\"panel\" method=\"post\"><h2>Admin Sign In</h2>";
	echo "<label for=\"admin_password\">Password</label><input id=\"admin_password\" name=\"admin_password\" type=\"password\" autocomplete=\"current-password\" required>";
	echo "<p><button type=\"submit\" name=\"admin_login\" value=\"1\">Sign In</button></p></form>";
	launcher_admin_render_footer();
	return;
}

try
{
	$db = launcher_database();
	launcher_admin_ensure_news_table($db);

	if($_SERVER["REQUEST_METHOD"] === "POST" && isset($_POST["action"]))
	{
		$action = $_POST["action"];
		if($action === "save")
		{
			$id = intval($_POST["id"] ?? 0);
			$title = trim($_POST["title"] ?? "");
			$summary = trim($_POST["summary"] ?? "");
			$body = trim($_POST["body"] ?? "");
			$bannerUrl = trim($_POST["banner_url"] ?? "");
			$linkUrl = trim($_POST["link_url"] ?? "");
			$publishedAt = launcher_admin_datetime_sql($_POST["published_at"] ?? "");
			$isPublished = isset($_POST["is_published"]) ? 1 : 0;
			$sortOrder = intval($_POST["sort_order"] ?? 0);

			if($title === "" || $summary === "") throw new Exception("Title and summary are required.");

			if($id > 0)
			{
				$statement = $db->prepare("UPDATE launcher_news SET title = ?, summary = ?, body = ?, banner_url = ?, link_url = ?, published_at = ?, is_published = ?, sort_order = ? WHERE id = ?");
				$statement->bind_param("ssssssiii", $title, $summary, $body, $bannerUrl, $linkUrl, $publishedAt, $isPublished, $sortOrder, $id);
				$statement->execute();
				$message = "News post updated.";
			}
			else
			{
				$statement = $db->prepare("INSERT INTO launcher_news (title, summary, body, banner_url, link_url, published_at, is_published, sort_order) VALUES (?, ?, ?, ?, ?, ?, ?, ?)");
				$statement->bind_param("ssssssii", $title, $summary, $body, $bannerUrl, $linkUrl, $publishedAt, $isPublished, $sortOrder);
				$statement->execute();
				$message = "News post created.";
			}
		}
		elseif($action === "delete")
		{
			$id = intval($_POST["id"] ?? 0);
			if($id <= 0) throw new Exception("A valid news id is required.");
			$statement = $db->prepare("DELETE FROM launcher_news WHERE id = ?");
			$statement->bind_param("i", $id);
			$statement->execute();
			$message = "News post deleted.";
		}
	}

	if($message !== "") echo "<div class=\"notice\">" . launcher_admin_escape($message) . "</div>";

	$editId = intval($_GET["edit"] ?? 0);
	$edit = array(
		"id" => 0,
		"title" => "",
		"summary" => "",
		"body" => "",
		"banner_url" => "",
		"link_url" => "",
		"published_at" => gmdate("Y-m-d H:i:s"),
		"is_published" => 1,
		"sort_order" => 0
	);

	if($editId > 0)
	{
		$statement = $db->prepare("SELECT id, title, summary, body, banner_url, link_url, published_at, is_published, sort_order FROM launcher_news WHERE id = ?");
		$statement->bind_param("i", $editId);
		$statement->execute();
		$result = $statement->get_result();
		$row = $result->fetch_assoc();
		if($row !== null) $edit = $row;
	}

	echo "<form class=\"panel\" method=\"post\"><h2>" . ($editId > 0 ? "Edit News Post" : "Add News Post") . "</h2>";
	echo "<input type=\"hidden\" name=\"action\" value=\"save\"><input type=\"hidden\" name=\"id\" value=\"" . intval($edit["id"]) . "\">";
	echo "<label for=\"title\">Title</label><input id=\"title\" name=\"title\" maxlength=\"160\" value=\"" . launcher_admin_escape($edit["title"]) . "\" required>";
	echo "<label for=\"summary\">Summary</label><textarea id=\"summary\" name=\"summary\" maxlength=\"500\" required>" . launcher_admin_escape($edit["summary"]) . "</textarea>";
	echo "<label for=\"body\">Body</label><textarea id=\"body\" name=\"body\">" . launcher_admin_escape($edit["body"]) . "</textarea>";
	echo "<div class=\"grid\"><div><label for=\"banner_url\">Banner URL</label><input id=\"banner_url\" name=\"banner_url\" maxlength=\"500\" value=\"" . launcher_admin_escape($edit["banner_url"]) . "\"></div>";
	echo "<div><label for=\"link_url\">Link URL</label><input id=\"link_url\" name=\"link_url\" maxlength=\"500\" value=\"" . launcher_admin_escape($edit["link_url"]) . "\"></div></div>";
	echo "<div class=\"grid\"><div><label for=\"published_at\">Published At</label><input id=\"published_at\" name=\"published_at\" type=\"datetime-local\" value=\"" . launcher_admin_escape(launcher_admin_datetime_value($edit["published_at"])) . "\"></div>";
	echo "<div><label for=\"sort_order\">Sort Order</label><input id=\"sort_order\" name=\"sort_order\" type=\"number\" value=\"" . intval($edit["sort_order"]) . "\"></div></div>";
	echo "<div class=\"checks\"><label><input type=\"checkbox\" name=\"is_published\" value=\"1\" " . (intval($edit["is_published"]) === 1 ? "checked" : "") . "> Published</label></div>";
	echo "<p class=\"actions\"><button type=\"submit\">Save News Post</button><a class=\"button secondary\" href=\"" . launcher_admin_escape($adminPath) . "\">New Post</a></p></form>";

	$result = $db->query("SELECT id, title, summary, published_at, is_published, sort_order FROM launcher_news ORDER BY sort_order ASC, published_at DESC, id DESC");
	echo "<h2>Existing Posts</h2><table><thead><tr><th>Title</th><th>Status</th><th>Published</th><th>Sort</th><th>Actions</th></tr></thead><tbody>";
	while($row = $result->fetch_assoc())
	{
		echo "<tr>";
		echo "<td><strong>" . launcher_admin_escape($row["title"]) . "</strong><div class=\"muted\">" . launcher_admin_escape($row["summary"]) . "</div></td>";
		echo "<td>" . (intval($row["is_published"]) === 1 ? "Published" : "Draft") . "</td>";
		echo "<td>" . launcher_admin_escape($row["published_at"]) . "</td>";
		echo "<td>" . intval($row["sort_order"]) . "</td>";
		echo "<td><div class=\"actions\"><a class=\"button secondary\" href=\"" . launcher_admin_escape($adminPath) . "?edit=" . intval($row["id"]) . "\">Edit</a>";
		echo "<form method=\"post\" onsubmit=\"return confirm('Delete this news post?');\"><input type=\"hidden\" name=\"action\" value=\"delete\"><input type=\"hidden\" name=\"id\" value=\"" . intval($row["id"]) . "\"><button class=\"danger\" type=\"submit\">Delete</button></form></div></td>";
		echo "</tr>";
	}
	echo "</tbody></table>";

	if($hasPassword)
	{
		echo "<form method=\"post\" class=\"panel\"><button class=\"secondary\" type=\"submit\" name=\"admin_logout\" value=\"1\">Sign Out</button></form>";
	}
}
catch(Exception $e)
{
	echo "<div class=\"error\">" . launcher_admin_escape($e->getMessage()) . "</div>";
}

launcher_admin_render_footer();

?>
