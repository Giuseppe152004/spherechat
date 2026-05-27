package com.proyecto.api.infrastructure.adapter.out.db

import cats.effect.IO
import com.proyecto.api.application.port.out.UserRepository
import com.proyecto.api.domain.model.*

class PostgresUserRepository(transactor: PostgresTransactor) extends UserRepository[IO]:

  override def findUser(docType: DocumentType, docNum: DocumentNumber, requestIp: String): IO[Option[User]] =
    transactor.connection.use { conn =>
      IO.blocking {
        val sql = """
          UPDATE training_dev.postulants 
          SET last_log = 'IP: ' || ? || ' | UA: Desktop App | Fecha: ' || NOW()
          WHERE document_type = ? 
            AND document_number = ?
          RETURNING id_postulant, first_name, last_name, remarks;
        """
        val stmt = conn.prepareStatement(sql)
        try
          stmt.setString(1, requestIp) // Usamos la IP real enviada por el frontend
          stmt.setString(2, docType.toString)
          stmt.setString(3, docNum.value)
          
          val rs = stmt.executeQuery()
          try
            if (rs.next()) {
              // Si el DNI existe en la tabla, el RETURNING devuelve datos y entra aquí.
              val nombres       = rs.getString("first_name")
              val apellidos     = rs.getString("last_name")
              val observaciones = Option(rs.getString("remarks"))
              Some(User(docType, docNum, nombres, apellidos, observaciones))
            } else {
              // Si el DNI no existe en la tabla, devuelve None (rechazado).
              None
            }
          finally rs.close()
        finally stmt.close()
      }
    }