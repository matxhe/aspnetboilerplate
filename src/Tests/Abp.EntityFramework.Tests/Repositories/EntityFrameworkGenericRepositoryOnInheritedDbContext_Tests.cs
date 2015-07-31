using Abp.Domain.Entities;
using Abp.Domain.Repositories;
using Abp.EntityFramework.Repositories;
using Abp.Tests;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Shouldly;
using Abp.EntityFramework.Dependency;
using Abp.Dependency;
using System.Reflection;
using Abp.Configuration.Startup;
using Castle.MicroKernel.Registration;
using NSubstitute;
using Abp.Domain.Uow;

namespace Abp.EntityFramework.Tests.Repositories
{
    public class EntityFrameworkGenericRepositoryOnInheritedDbContext_Tests : TestBaseWithLocalIocManager
    {

        [Fact]
        public void Should_Be_Join_Supported()
        {
            //Arrange
            new EntityFrameworkConventionalRegisterer()
                .RegisterAssembly(
                    new ConventionalRegistrationContext(
                        Assembly.GetExecutingAssembly(),
                        LocalIocManager,
                        new ConventionalRegistrationConfig()
                        ));

            

            LocalIocManager.Register<IAbpStartupConfiguration, AbpStartupConfiguration>();
            LocalIocManager.Resolve<IAbpStartupConfiguration>().DefaultNameOrConnectionString = "Server=localhost;Database=test;User=sa;Password=123";

            EntityFrameworkGenericRepositoryRegistrar.RegisterForDbContext(typeof(MyModuleDbContext), LocalIocManager);
            EntityFrameworkGenericRepositoryRegistrar.RegisterForDbContext(typeof(MyMainDbContext), LocalIocManager);

            var myMainDbContext = LocalIocManager.Resolve<MyMainDbContext>();
            var fakeMainDbContextProvider = NSubstitute.Substitute.For<IDbContextProvider<MyMainDbContext>>(myMainDbContext);
            var myModuleDbContext = LocalIocManager.Resolve<MyModuleDbContext>();
            var fakeModuleDbContextProvider = NSubstitute.Substitute.For<IDbContextProvider<MyModuleDbContext>>(myModuleDbContext);

            var fakeUow = Substitute.For<IUnitOfWork>();

            LocalIocManager.IocContainer.Register(
                Component.For<IUnitOfWorkDefaultOptions>().ImplementedBy<UnitOfWorkDefaultOptions>().LifestyleSingleton(),
                Component.For<ICurrentUnitOfWorkProvider>().ImplementedBy<CallContextCurrentUnitOfWorkProvider>().LifestyleSingleton(),
                Component.For<IUnitOfWorkManager>().ImplementedBy<UnitOfWorkManager>().LifestyleSingleton(),
                Component.For<IUnitOfWork>().UsingFactoryMethod(() => fakeUow).LifestyleSingleton()
                );
            var uowProvider = LocalIocManager.Resolve<ICurrentUnitOfWorkProvider>();
            uowProvider.re
            LocalIocManager.IocContainer.Register(
                Component.For<IDbContextProvider<MyMainDbContext>>().UsingFactoryMethod(() => fakeMainDbContextProvider),
                Component.For<IDbContextProvider<MyModuleDbContext>>().UsingFactoryMethod(() => fakeModuleDbContextProvider)
                );




            //Entity 1 (with default PK)
            var entity1Repository = LocalIocManager.Resolve<IRepository<MyEntity1>>();
            var entity1 = new MyEntity1()
            {
                Id = 1,
                Name1 = "one"
            };
            entity1Repository.Insert(entity1);
            //Entity 2
            var entity2Repository = LocalIocManager.Resolve<IRepository<MyEntity2, long>>();
            var entity2 = new MyEntity2()
            {
                Id = 2,
                MyEntity1Id = 1,
                Name2 = "two"
            };
            entity2Repository.Insert(entity2);
            //Entity 3
            var entity3Repository = LocalIocManager.Resolve<IMyModuleRepository<MyEntity3, Guid>>();
            var entity3 = new MyEntity3()
            {
                Id = Guid.NewGuid(),
                MyEntity2Id = 2,
                Name3 = "three"
            };
            entity3Repository.Insert(entity3);

            //Act
            var result = from one in entity1Repository.GetAll()
                         from two in entity2Repository.GetAll()
                         from three in entity3Repository.GetAll()
                         where one.Id == two.MyEntity1Id && two.Id == three.MyEntity2Id
                         select new
                         {
                             Name1 = one.Name1,
                             Name2 = two.Name2,
                             Name3 = three.Name3
                         };

            //Assert
            result.ShouldAllBe(a => a.Name1 == "one" && a.Name2 == "two" && a.Name3 == "three");
        }

        public class MyMainDbContext : MyBaseDbContext
        {
            public virtual DbSet<MyEntity2> MyEntities2 { get; set; }
        }

        [AutoRepositoryTypes(
            typeof(IMyModuleRepository<>),
            typeof(IMyModuleRepository<,>),
            typeof(MyModuleRepositoryBase<>),
            typeof(MyModuleRepositoryBase<,>)
            )]
        public class MyModuleDbContext : MyBaseDbContext
        {
            public virtual DbSet<MyEntity3> MyEntities3 { get; set; }
        }

        public abstract class MyBaseDbContext : AbpDbContext
        {
            public virtual IDbSet<MyEntity1> MyEntities1 { get; set; }
        }

        public class MyEntity1 : Entity
        {
            public string Name1 { get; set; }
        }

        public class MyEntity2 : Entity<long>
        {
            public int MyEntity1Id { get; set; }
            public string Name2 { get; set; }
        }

        public class MyEntity3 : Entity<Guid>
        {
            public long MyEntity2Id { get; set; }
            public string Name3 { get; set; }
        }

        public interface IMyModuleRepository<TEntity> : IRepository<TEntity>
            where TEntity : class, IEntity<int>
        {

        }

        public interface IMyModuleRepository<TEntity, TPrimaryKey> : IRepository<TEntity, TPrimaryKey>
            where TEntity : class, IEntity<TPrimaryKey>
        {

        }

        public class MyModuleRepositoryBase<TEntity, TPrimaryKey> : EfRepositoryBase<MyModuleDbContext, TEntity, TPrimaryKey>, IMyModuleRepository<TEntity, TPrimaryKey>
            where TEntity : class, IEntity<TPrimaryKey>
        {
            public MyModuleRepositoryBase(IDbContextProvider<MyModuleDbContext> dbContextProvider)
                : base(dbContextProvider)
            {
            }
        }

        public class MyModuleRepositoryBase<TEntity> : MyModuleRepositoryBase<TEntity, int>, IMyModuleRepository<TEntity>
            where TEntity : class, IEntity<int>
        {
            public MyModuleRepositoryBase(IDbContextProvider<MyModuleDbContext> dbContextProvider)
                : base(dbContextProvider)
            {
            }
        }
    }
}
