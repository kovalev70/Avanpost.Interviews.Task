using Avanpost.Interviews.Task.Integration.Data.DbCommon;
using Avanpost.Interviews.Task.Integration.Data.DbCommon.DbModels;
using Avanpost.Interviews.Task.Integration.Data.Models;
using Avanpost.Interviews.Task.Integration.Data.Models.Models;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Avanpost.Interviews.Task.Integration.SandBox.Connector
{
    public class ConnectorDb : IConnector
    {
        private DataContext _context;

        public ILogger Logger { get; set; }

        public void StartUp(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string is required.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
            var connectionStringBuilder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (connectionStringBuilder["Provider"].ToString()!.Contains("PostgreSQL"))
            {
                optionsBuilder.UseNpgsql(connectionStringBuilder["ConnectionString"].ToString());
            }
            else
            {
                throw new NotSupportedException("Unsupported database type.");
            }

            _context = new DataContext(optionsBuilder.Options);
        }

        public void CreateUser(UserToCreate user)
        {
            try
            {
                if (user == null)
                {
                    throw new ArgumentNullException(nameof(user), "User object cannot be null.");
                }

                Dictionary<string, string> properties = user.Properties.ToDictionary
                    (x => x.Name,
                    x => x.Value);

                _context.Users.Add(new User
                {
                    Login = user.Login,
                    LastName = properties.GetValueOrDefault("LastName", string.Empty),
                    FirstName = properties.GetValueOrDefault("FirstName", string.Empty),
                    MiddleName = properties.GetValueOrDefault("MiddleName", string.Empty),
                    TelephoneNumber = properties.GetValueOrDefault("TelephoneNumber", string.Empty),
                    IsLead = bool.Parse(properties.GetValueOrDefault("IsLead", "false"))
                });

                _context.Passwords.Add(new Sequrity
                {
                    UserId = user.Login,
                    Password = user.HashPassword
                });

                _context.SaveChanges();
            }
            catch (Exception ex) 
            {
                Logger.Error(ex.Message);
                throw;
            }
        }

        public IEnumerable<Property> GetAllProperties()
        {
            var properties = _context.Users.EntityType.GetProperties()
                .Where(x => x.Name != "Login" && x.Name != "IsLead")
                .Select(x => new Property(x.Name, string.Empty))
                .ToList();

            properties.AddRange(
                _context.Passwords.EntityType.GetProperties()
                .Where(x => x.Name != "Id" && x.Name != "UserId")
                .Select(x => new Property(x.Name, string.Empty))
                .ToList());

            return properties;
        }

        public IEnumerable<UserProperty> GetUserProperties(string userLogin)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(x => x.Login == userLogin) 
                    ?? throw new Exception("The user was not found.");

                var password = _context.Passwords.FirstOrDefault(x => x.UserId == userLogin) 
                    ?? throw new Exception("The password was not found.");

                return new List<UserProperty>
                {
                new UserProperty("LastName", user.LastName),
                new UserProperty("FirstName", user.FirstName),
                new UserProperty("MiddleName", user.MiddleName),
                new UserProperty("TelephoneNumber", user.TelephoneNumber),
                new UserProperty("Password", password.Password)
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                throw;
            }
        }

        public bool IsUserExists(string userLogin)
        {
            return _context.Users.Any(x => x.Login == userLogin);
        }

        public void UpdateUserProperties(IEnumerable<UserProperty> properties, string userLogin)
        {
            try 
            {
                var user = _context.Users.FirstOrDefault(x => x.Login == userLogin) 
                    ?? throw new Exception("The user was not found.");

                var password = _context.Passwords.FirstOrDefault(x => x.UserId == userLogin) 
                    ?? throw new Exception("The password was not found.");

                foreach (var property in properties) 
                {
                    if (property.Name.ToLower() == "password")
                    {
                        password.Password = property.Value;
                    }
                    else
                    {
                        _context.Entry(user).Property(property.Name).CurrentValue = property.Value;
                    }
                }

                _context.SaveChanges();
            }

            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                throw;
            }
        }

        public IEnumerable<Permission> GetAllPermissions()
        {
            var permissionsRequestRights = _context.RequestRights
                .Select(x => new Permission(x.Id.ToString()!, x.Name, string.Empty))
                .ToList();

            var permissionsItRoles = _context.ITRoles
                .Select(x => new Permission(x.Id.ToString()!, x.Name, string.Empty))
                .ToList();

            return permissionsRequestRights.Concat(permissionsItRoles).ToList();
        }

        public void AddUserPermissions(string userLogin, IEnumerable<string> rightIds)
        {
            try 
            {
                var userRights = _context.UserRequestRights.Where(e => e.UserId == userLogin).ToList();
                var userRoles = _context.UserITRoles.Where(e => e.UserId == userLogin).ToList();

                foreach (string rightId in rightIds)
                {
                    int databaseId = int.Parse(rightId.Split(":")[1]);

                    if (rightId.StartsWith("Role"))
                    {
                        _context.UserITRoles.Add
                            (new UserITRole() { UserId = userLogin, RoleId = databaseId });
                    }
                    else if (rightId.StartsWith("Request"))
                    {
                        _context.UserRequestRights.Add
                            (new UserRequestRight() { UserId = userLogin, RightId = databaseId });
                    }
                }

                _context.SaveChanges();
            }
            catch (Exception ex) 
            {
                Logger.Error(ex.Message);
                throw;
            }
        }

        public void RemoveUserPermissions(string userLogin, IEnumerable<string> rightIds)
        {
            try
            {
                foreach (string rightId in rightIds)
                {
                    int databaseId = int.Parse(rightId.Split(":")[1]);

                    if (rightId.StartsWith("Role"))
                    {
                        var role = _context.UserITRoles.FirstOrDefault(r => r.UserId == userLogin && r.RoleId == databaseId);
                        if (role != null)
                        {
                            _context.UserITRoles.Remove(role);
                        }
                    }
                    else if (rightId.StartsWith("Request"))
                    {
                        var request = _context.UserRequestRights.FirstOrDefault(r => r.UserId == userLogin && r.RightId == databaseId);
                        if (request != null)
                        {
                            _context.UserRequestRights.Remove(request);
                        }
                    }
                }

                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                throw;
            }
        }

        public IEnumerable<string> GetUserPermissions(string userLogin)
        {
            var userPermissions = new List<string>();

            var userRoles = _context.UserITRoles.Where(e => e.UserId == userLogin).Select(r => $"Role:{r.RoleId}");
            var userRights = _context.UserRequestRights.Where(e => e.UserId == userLogin).Select(r => $"Request:{r.RightId}");

            userPermissions.AddRange(userRoles);
            userPermissions.AddRange(userRights);

            return userPermissions;
        }
    }
}