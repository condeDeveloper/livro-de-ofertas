namespace LivroDeOfertas.Core.Dominio;

/// <summary>Regra de permanência da ordem (time in force).</summary>
public enum Validade
{
    /// <summary>Fica no livro até ser executada ou cancelada (GTC).</summary>
    AteCancelar,

    /// <summary>Executa o que puder imediatamente e cancela o restante (IOC).</summary>
    ExecutaOuCancela,

    /// <summary>Só executa se puder ser totalmente preenchida de imediato; senão é cancelada inteira (FOK).</summary>
    TudoOuNada,
}
