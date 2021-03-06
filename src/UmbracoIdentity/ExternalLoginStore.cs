using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Microsoft.AspNet.Identity;
using Umbraco.Core;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseAnnotations;
using UmbracoIdentity.Models;

namespace UmbracoIdentity
{
    /// <summary>
    ///     We are using a custom sqlce db to store this information
    /// </summary>
    internal class ExternalLoginStore : DisposableObject, IExternalLoginStore
    {

        //TODO: What is the OWIN form of MapPath??

        public static readonly object Locker = new object();
        private readonly UmbracoDatabase _db;

        private readonly ConnectionStringSettings identityStoreConnection = ConfigurationManager.ConnectionStrings["UmbracoIdentityStore"];

        public ExternalLoginStore()
        {
            if (identityStoreConnection == null)
            {
                throw new ConfigurationException("UmbracoIdentityStore is not set in the configuration");
            }

            _db = new UmbracoDatabase(identityStoreConnection.ConnectionString, "System.Data.SqlClient");
            if (!_db.TableExist("ExternalLogins"))
            {
                //unfortunately we'll get issues if we just try this because of differing sql syntax providers. In newer
                // umbraco versions we'd just use the DatabaseSchemaHelper. So in the meantime we have to just 
                // do this manually and use reflection :(;
                _db.CreateTable<ExternalLoginDto>();
            }
        }

        public IEnumerable<IdentityMemberLogin<int>> GetAll(int userId)
        {
            var sql = new Sql()
                .Select("*")
                .From<ExternalLoginDto>()
                .Where<ExternalLoginDto>(dto => dto.UserId == userId);

            var found = _db.Fetch<ExternalLoginDto>(sql);

            return found.Select(x => new IdentityMemberLogin<int>
            {
                LoginProvider = x.LoginProvider,
                ProviderKey = x.ProviderKey,
                UserId = x.UserId
            });
        }

        public IEnumerable<int> Find(UserLoginInfo login)
        {
            var sql = new Sql()
                .Select("*")
                .From<ExternalLoginDto>()
                .Where<ExternalLoginDto>(dto => dto.LoginProvider == login.LoginProvider && dto.ProviderKey == login.ProviderKey);

            var found = _db.Fetch<ExternalLoginDto>(sql);

            return found.Select(x => x.UserId);
        }

        public void SaveUserLogins(int memberId, IEnumerable<UserLoginInfo> logins)
        {
            using (var t = _db.GetTransaction())
            {
                //clear out logins for member
                _db.Execute("DELETE FROM ExternalLogins WHERE UserId=@userId", new { userId = memberId });

                //add them all
                foreach (var l in logins)
                {
                    _db.Insert(new ExternalLoginDto
                    {
                        LoginProvider = l.LoginProvider,
                        ProviderKey = l.ProviderKey,
                        UserId = memberId
                    });
                }

                t.Complete();
            }
        }

        public void DeleteUserLogins(int memberId)
        {
            using (var t = _db.GetTransaction())
            {
                _db.Execute("DELETE FROM ExternalLogins WHERE UserId=@userId", new { userId = memberId });

                t.Complete();
            }
        }

        protected override void DisposeResources()
        {
            _db.Dispose();
        }

        [TableName("ExternalLogins")]
        [ExplicitColumns]
        [PrimaryKey("ExternalLoginId")]
        internal class ExternalLoginDto
        {

            [Column("ExternalLoginId")]
            [PrimaryKeyColumn(Name = "PK_ExternalLoginId")]
            public int ExternalLoginId { get; set; }

            [Column("UserId")]
            public int UserId { get; set; }

            [Column("LoginProvider")]
            [Length(4000)]
            [NullSetting(NullSetting = NullSettings.NotNull)]
            public string LoginProvider { get; set; }

            [Column("ProviderKey")]
            [Length(4000)]
            [NullSetting(NullSetting = NullSettings.NotNull)]
            public string ProviderKey { get; set; }

        }

    }
}
