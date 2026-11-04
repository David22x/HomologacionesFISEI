using BuildingBlocks.Roles;
using Xunit;

namespace BuildingBlocks.Tests;

public class AppRolesTests
{
    [Fact]
    public void AdministradorRole_HasExpectedValue()
    {
        Assert.Equal("Administrador", AppRoles.Administrador);
    }

    [Fact]
    public void ProfesorRole_HasExpectedValue()
    {
        Assert.Equal("Profesor", AppRoles.Profesor);
    }
}
