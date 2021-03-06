﻿using System.Collections.Generic;
using Microsoft.AspNet.WebHooks;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Events;
using Nop.Core.Infrastructure;
using Nop.Plugin.Api.Services;
using Nop.Services.Events;
using Nop.Plugin.Api.DTOs.Customers;
using Nop.Plugin.Api.Constants;
using Nop.Plugin.Api.DTOs.Products;
using Nop.Plugin.Api.Helpers;
using Nop.Plugin.Api.DTOs.Categories;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.Api.DTOs.Orders;
using Nop.Plugin.Api.DTOs.Stores;
using Nop.Plugin.Api.MappingExtensions;
using Nop.Services.Stores;

namespace Nop.Plugin.Api.WebHooks
{
    public class WebHookEventConsumer : IConsumer<EntityInserted<Customer>>,
        IConsumer<EntityUpdated<Customer>>,
        IConsumer<EntityInserted<Product>>,
        IConsumer<EntityUpdated<Product>>,
        IConsumer<EntityInserted<Category>>,
        IConsumer<EntityUpdated<Category>>,
        IConsumer<EntityInserted<Order>>,
        IConsumer<EntityUpdated<Order>>,
        IConsumer<EntityInserted<StoreMapping>>,
        IConsumer<EntityDeleted<StoreMapping>>,
        IConsumer<EntityInserted<GenericAttribute>>,
        IConsumer<EntityUpdated<Store>>
    {
        private IWebHookManager _webHookManager;
        private ICustomerApiService _customerApiService;
        private ICategoryApiService _categoryApiService;
        private IProductApiService _productApiService;
        private IStoreService _storeService;

        private IDTOHelper _dtoHelper;

        public WebHookEventConsumer(IStoreService storeService)
        {
            IWebHookService webHookService = EngineContext.Current.ContainerManager.Resolve<IWebHookService>();
            _customerApiService = EngineContext.Current.ContainerManager.Resolve<ICustomerApiService>();
            _categoryApiService = EngineContext.Current.ContainerManager.Resolve<ICategoryApiService>();
            _productApiService = EngineContext.Current.ContainerManager.Resolve<IProductApiService>();
            _dtoHelper = EngineContext.Current.ContainerManager.Resolve<IDTOHelper>();
            _storeService = EngineContext.Current.ContainerManager.Resolve<IStoreService>();

            _webHookManager = webHookService.GetHookManager();
        }

        public void HandleEvent(EntityInserted<Customer> eventMessage)
        {
            // There is no need to send webhooks for guest customers.
            if (eventMessage.Entity.IsGuest())
            {
                return;
            }

            CustomerDto customer = _customerApiService.GetCustomerById(eventMessage.Entity.Id);
            var storeIds = new List<int>();

            if (customer.RegisteredInStoreId.HasValue)
            {
                storeIds.Add(customer.RegisteredInStoreId.Value);
            }

            NotifyRegisteredWebHooks(customer, WebHookNames.CustomerCreated, storeIds);
        }

        public void HandleEvent(EntityUpdated<Customer> eventMessage)
        {
            // There is no need to send webhooks for guest customers.
            if (eventMessage.Entity.IsGuest())
            {
                return;
            }

            CustomerDto customer = _customerApiService.GetCustomerById(eventMessage.Entity.Id, true);

            // In nopCommerce the Customer, Product, Category and Order entities are not deleted.
            // Instead the Deleted property of the entity is set to true.
            string webhookEvent = WebHookNames.CustomerUpdated;

            if (customer.Deleted == true)
            {
                webhookEvent = WebHookNames.CustomerDeleted;
            }

            var storeIds = new List<int>();

            if (customer.RegisteredInStoreId.HasValue)
            {
                storeIds.Add(customer.RegisteredInStoreId.Value);
            }

            NotifyRegisteredWebHooks(customer, webhookEvent, storeIds);
        }

        public void HandleEvent(EntityInserted<Product> eventMessage)
        {
            ProductDto productDto = _dtoHelper.PrepareProductDTO(eventMessage.Entity);

            // The Store mappings of the product are still not saved, so all webhooks will be triggered
            // no matter for which store are registered.
            NotifyRegisteredWebHooks(productDto, WebHookNames.ProductCreated, productDto.StoreIds);
        }

        public void HandleEvent(EntityUpdated<Product> eventMessage)
        {
            ProductDto productDto = _dtoHelper.PrepareProductDTO(eventMessage.Entity);

            string webhookEvent = WebHookNames.ProductUpdated;

            if (productDto.Deleted == true)
            {
                webhookEvent = WebHookNames.ProductDeleted;
            }

            NotifyRegisteredWebHooks(productDto, webhookEvent, productDto.StoreIds);
        }

        public void HandleEvent(EntityInserted<Category> eventMessage)
        {
            CategoryDto categoryDto = _dtoHelper.PrepareCategoryDTO(eventMessage.Entity);

            // The Store mappings of the category are still not saved, so all webhooks will be triggered
            // no matter for which store are registered.
            NotifyRegisteredWebHooks(categoryDto, WebHookNames.CategoryCreated, categoryDto.StoreIds);
        }

        public void HandleEvent(EntityUpdated<Category> eventMessage)
        {
            CategoryDto categoryDto = _dtoHelper.PrepareCategoryDTO(eventMessage.Entity);

            string webhookEvent = WebHookNames.CategoryUpdated;

            if (categoryDto.Deleted == true)
            {
                webhookEvent = WebHookNames.CategoryDeleted;
            }

            NotifyRegisteredWebHooks(categoryDto, webhookEvent, categoryDto.StoreIds);
        }

        public void HandleEvent(EntityInserted<Order> eventMessage)
        {
            OrderDto orderDto = _dtoHelper.PrepareOrderDTO(eventMessage.Entity);

            var storeIds = new List<int>();

            if (orderDto.StoreId.HasValue)
            {
                storeIds.Add(orderDto.StoreId.Value);
            }

            NotifyRegisteredWebHooks(orderDto, WebHookNames.OrderCreated, storeIds);
        }

        public void HandleEvent(EntityUpdated<Order> eventMessage)
        {
            OrderDto orderDto = _dtoHelper.PrepareOrderDTO(eventMessage.Entity);

            string webhookEvent = WebHookNames.OrderUpdated;

            if (orderDto.Deleted == true)
            {
                webhookEvent = WebHookNames.OrderDeleted;
            }

            var storeIds = new List<int>();

            if (orderDto.StoreId.HasValue)
            {
                storeIds.Add(orderDto.StoreId.Value);
            }

            NotifyRegisteredWebHooks(orderDto, webhookEvent, storeIds);
        }

        public void HandleEvent(EntityInserted<StoreMapping> eventMessage)
        {
            HandleStoreMappingEvent(eventMessage.Entity.EntityId, eventMessage.Entity.EntityName);
        }

        public void HandleEvent(EntityDeleted<StoreMapping> eventMessage)
        {
            HandleStoreMappingEvent(eventMessage.Entity.EntityId, eventMessage.Entity.EntityName);
        }

        public void HandleEvent(EntityInserted<GenericAttribute> eventMessage)
        {
            if (eventMessage.Entity.Key == SystemCustomerAttributeNames.FirstName ||
                eventMessage.Entity.Key == SystemCustomerAttributeNames.LastName ||
                eventMessage.Entity.Key == SystemCustomerAttributeNames.LanguageId)
            {
                var customerDto = _customerApiService.GetCustomerById(eventMessage.Entity.EntityId);

                var storeIds = new List<int>();

                if (customerDto.RegisteredInStoreId.HasValue)
                {
                    storeIds.Add(customerDto.RegisteredInStoreId.Value);
                }

                NotifyRegisteredWebHooks(customerDto, WebHookNames.CustomerUpdated, storeIds);
            }
        }

        public void HandleEvent(EntityUpdated<Store> eventMessage)
        {
            StoreDto storeDto = eventMessage.Entity.ToDto();

            int storeId;

            if (int.TryParse(storeDto.Id, out storeId))
            {
                var storeIds = new List<int>();
                storeIds.Add(storeId);

                NotifyRegisteredWebHooks(storeDto, WebHookNames.StoreUpdated, storeIds);
            }
        }

        private void HandleStoreMappingEvent(int entityId, string entityName)
        {
            // When creating or editing a category after saving the store mapping the category is not updated
            // so we should listen for StoreMapping update/delete and fire a webhook with the updated entityDto(with correct storeIds).
            if (entityName == "Category")
            {
                var category = _categoryApiService.GetCategoryById(entityId);

                if (category != null)
                {
                    CategoryDto categoryDto = _dtoHelper.PrepareCategoryDTO(category);

                    string webhookEvent = WebHookNames.CategoryUpdated;

                    if (categoryDto.Deleted == true)
                    {
                        webhookEvent = WebHookNames.CategoryDeleted;
                    }

                    NotifyRegisteredWebHooks(categoryDto, webhookEvent, categoryDto.StoreIds);
                }
            }
            else if (entityName == "Product")
            {
                var product = _productApiService.GetProductById(entityId);

                if (product != null)
                {
                    ProductDto productDto = _dtoHelper.PrepareProductDTO(product);

                    string webhookEvent = WebHookNames.ProductUpdated;

                    if (productDto.Deleted == true)
                    {
                        webhookEvent = WebHookNames.ProductDeleted;
                    }

                    NotifyRegisteredWebHooks(productDto, webhookEvent, productDto.StoreIds);
                }
            }
        }

        private void NotifyRegisteredWebHooks<T>(T entityDto, string webhookEvent, List<int> storeIds)
        {
            if (storeIds.Count > 0)
            {
                // Notify all webhooks that the entity is mapped to their store.
                _webHookManager.NotifyAllAsync(webhookEvent, new { Item = entityDto }, (hook, hookUser) => IsEntityMatchingTheWebHookStoreId(hookUser, storeIds));

                if (typeof(T) == typeof(ProductDto) || typeof(T) == typeof(CategoryDto))
                {
                    NotifyUnmappedEntityWebhooks(entityDto, storeIds);
                }
            }
            else
            {
                _webHookManager.NotifyAllAsync(webhookEvent, new { Item = entityDto });
            }
        }

        private void NotifyUnmappedEntityWebhooks<T>(T entityDto, List<int> storeIds)
        {
            if (typeof(T) == typeof(ProductDto))
            {
                // The product is not mapped to the store.
                // Notify all webhooks that the entity is not mapped to their store.
                _webHookManager.NotifyAllAsync(WebHookNames.ProductUnmapped, new { Item = entityDto },
                    (hook, hookUser) => !IsEntityMatchingTheWebHookStoreId(hookUser, storeIds));
            }
            else if (typeof(T) == typeof(CategoryDto))
            {
                // The category is not mapped to the store.
                // Notify all webhooks that the entity is not mapped to their store.
                _webHookManager.NotifyAllAsync(WebHookNames.CategoryUnmapped, new { Item = entityDto },
                    (hook, hookUser) => !IsEntityMatchingTheWebHookStoreId(hookUser, storeIds));
            }
        }

        private bool IsEntityMatchingTheWebHookStoreId(string webHookUser, List<int> storeIds)
        {
            // When we register the webhooks we add "-storeId" at the end of the webHookUser string.
            // That way we can check to which store is mapped the webHook.
            foreach (var id in storeIds)
            {
                if (webHookUser.EndsWith("-" + id))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
