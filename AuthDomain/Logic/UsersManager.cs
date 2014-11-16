﻿using AuthDomain.Dal;
using AuthDomain.Models.Account;
using AuthDomain.Resources;
using System.Data;
using System.Threading.Tasks;

namespace AuthDomain.Logic
{
    public class UsersManager
    {
        public IUsersDal UsersDal { get; set; }

        public UsersManager()
        {
            this.UsersDal = new UsersDal();
        }

        public Task<User> GetUserAsync(int userId)
        {
            return Task<User>.Factory.StartNew(() =>
            {
                return this.UsersDal.Execute(IsolationLevel.ReadCommitted,
                (tran) =>
                {
                    return this.UsersDal.GetUser(tran, userId);
                });
            });
        }

        public Task<User> GetUserAsync(ExternalLoginProvider loginProvider, string providerKey)
        {
            return Task<User>.Factory.StartNew(() =>
            {
                return this.UsersDal.Execute(IsolationLevel.ReadCommitted,
                (tran) =>
                {
                    return this.UsersDal.GetUser(tran, loginProvider, providerKey);
                });
            });
        }

        public Task<byte[]> GetAvatarAsync(int userId)
        {
            return Task<byte[]>.Factory.StartNew(() =>
            {
                return this.UsersDal.Execute(IsolationLevel.ReadCommitted,
                (tran) =>
                {
                    return this.UsersDal.GetAvatar(tran, userId);
                });
            });
        }

        public Task<User> CreateUserAsync(UserRegistration userRegistration)
        {
            return Task<User>.Factory.StartNew(() =>
            {
                return this.UsersDal.Execute(IsolationLevel.Serializable,
                (tran) =>
                {
                    //Check if external login is unique
                    if (userRegistration.ExternalLoginInfo != null)
                    {
                        if (this.UsersDal.GetUser(tran,
                            userRegistration.ExternalLoginInfo.ProviderType,
                            userRegistration.ExternalLoginInfo.ProviderKey) != null)

                            throw new ApiException(string.Format(Exceptions.ExternalLoginAlreadyExists,
                                userRegistration.ExternalLoginInfo.ProviderType));
                    }

                    //Check login is unique if registration via password
                    UserDb dbUser = this.UsersDal.GetUser(tran, userRegistration.Email);
                    if (dbUser != null && !string.IsNullOrEmpty(dbUser.Password) && !string.IsNullOrEmpty(userRegistration.Password))
                        throw new ApiException(string.Format(Exceptions.UserAlreadyRegistered, userRegistration.Email));

                    //Create user if doesn't exist
                    bool isNewUser = (dbUser == null);
                    if (isNewUser)
                        dbUser = this.UsersDal.CreateUser(tran, userRegistration);

                    //If external, create extLogin record
                    if (userRegistration.ExternalLoginInfo != null)
                        this.UsersDal.CreateUserExtLoginInfo(tran, dbUser.Id, userRegistration.ExternalLoginInfo);

                    //If avatar specified -> save to DB
                    if (userRegistration.Avatar != null)
                    {
                        if (isNewUser)
                            this.UsersDal.CreateUserAvatar(tran, dbUser.Id, userRegistration.Avatar);
                        else
                            this.UsersDal.UpdateUserAvatar(tran, dbUser.Id, userRegistration.Avatar);
                    }

                    return dbUser;
                });
            });
        }

        public Task<UserVerification> VerifyAsync(int userId)
        {
            return Task<UserVerification>.Factory.StartNew(() =>
            {
                return this.UsersDal.Execute(IsolationLevel.ReadCommitted,
                (tran) =>
                {
                    var user = this.UsersDal.GetUser(tran, userId);

                    if (user.IsVerified)
                        throw new ApiException(Exceptions.UserAlreadyVerified);

                    return new UserVerification()
                    {
                        User = user,
                        VerifyEmailCode = user.VerifyEmailCode.Value
                    };
                });
            });
        }

        public Task DeleteUserWithDependenciesAsync(int userId)
        {
            return Task.Factory.StartNew(() =>
            {
                this.UsersDal.Execute(IsolationLevel.Snapshot,
                (tran) =>
                {
                    this.UsersDal.DeleteUserWithDependencies(tran, userId);
                });
            });
        }
    }
}
