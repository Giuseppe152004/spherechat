name := "mi-api-scala"
version := "0.1.0-SNAPSHOT"
scalaVersion := "3.3.7" // Actualizado para soportar Metals

Compile / run / fork := true

val tapirVersion = "1.9.8"
val http4sVersion = "0.23.26"
val circeVersion = "0.14.6"
val catsEffectVersion = "3.5.4"
val skunkVersion = "0.6.3" // Driver funcional puro para Postgres

libraryDependencies ++= Seq(
  // Programación Funcional
  "org.typelevel" %% "cats-effect" % catsEffectVersion,
  
  // Http4s para REST APIs (Usado por Chat y Tapir)
  "org.http4s" %% "http4s-ember-server" % http4sVersion,
  "org.http4s" %% "http4s-dsl"          % http4sVersion,
  "org.http4s" %% "http4s-circe"        % http4sVersion,
  
  // Tapir y OpenAPI (Módulos Legacy Auth/Capacitacion)
  "com.softwaremill.sttp.tapir" %% "tapir-core" % tapirVersion,
  "com.softwaremill.sttp.tapir" %% "tapir-http4s-server" % tapirVersion,
  "com.softwaremill.sttp.tapir" %% "tapir-json-circe" % tapirVersion,
  "com.softwaremill.sttp.tapir" %% "tapir-openapi-docs" % tapirVersion,
  "com.softwaremill.sttp.apispec" %% "openapi-circe" % "0.7.3",
  
  // Circe para JSON / JSONB
  "io.circe" %% "circe-core"    % circeVersion,
  "io.circe" %% "circe-generic" % circeVersion,
  "io.circe" %% "circe-parser"  % circeVersion,
  
  // Skunk: Postgres driver no bloqueante para Cats Effect (Chat)
  "org.tpolecat" %% "skunk-core"  % skunkVersion,
  "org.tpolecat" %% "skunk-circe" % skunkVersion,
  
  // Seguridad y Hashing
  "at.favre.lib" % "bcrypt" % "0.10.2",
  
  // JDBC Tradicional (Módulos Legacy)
  "org.postgresql" % "postgresql" % "42.7.2",
  
  "com.auth0" % "java-jwt" % "4.4.0",
  "ch.qos.logback" % "logback-classic" % "1.5.3",
  
  // Testing
  "org.scalameta" %% "munit" % "1.0.0" % Test,
  "org.typelevel" %% "munit-cats-effect-3" % "1.0.7" % Test
)