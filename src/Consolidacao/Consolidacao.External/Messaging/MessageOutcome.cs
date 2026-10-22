namespace Consolidacao.External.Messaging;

public enum MessageOutcome
{
    /// <summary>Processada (ou duplicata): confirma e remove da fila.</summary>
    Ack = 1,

    /// <summary>Mensagem inválida ("poison"): vai direto para a DLQ, sem novas tentativas.</summary>
    Reject = 2,

    /// <summary>Falha transitória: reenfileira com contador de tentativas; ao esgotá-las a mensagem vai para a DLQ.</summary>
    Retry = 3,
}
