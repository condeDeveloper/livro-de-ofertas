namespace LivroDeOfertas.Core.Dominio;

/// <summary>Lado da ordem no livro.</summary>
public enum Lado
{
    Compra,
    Venda,
}

public static class LadoExtensoes
{
    public static Lado Oposto(this Lado lado) => lado == Lado.Compra ? Lado.Venda : Lado.Compra;
}
