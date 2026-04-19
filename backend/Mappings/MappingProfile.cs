using AutoMapper;
using MyGarage.Models;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // AiForecast Mappings
            CreateMap<AiForecast, AiForecastResponseDto>();
            CreateMap<AiForecastCreateDto, AiForecast>();
            CreateMap<AiForecastUpdateDto, AiForecast>();
            // AiKnowledgeBase Mappings
            CreateMap<AiKnowledgeBase, AiKnowledgeBaseResponseDto>();
            CreateMap<AiKnowledgeBaseCreateDto, AiKnowledgeBase>();
            CreateMap<AiKnowledgeBaseUpdateDto, AiKnowledgeBase>();
            // AiModelConfig Mappings
            CreateMap<AiModelConfig, AiModelConfigResponseDto>()
                .ForMember(dest => dest.HasApiKey, opt =>
                    opt.MapFrom(src => !string.IsNullOrWhiteSpace(src.ApiKey)));
            CreateMap<AiModelConfigCreateDto, AiModelConfig>();
            CreateMap<AiModelConfigUpdateDto, AiModelConfig>();
            // AiSmartDispatching Mappings
            CreateMap<AiSmartDispatching, AiSmartDispatchingResponseDto>();
            CreateMap<AiSmartDispatchingCreateDto, AiSmartDispatching>();
            CreateMap<AiSmartDispatchingUpdateDto, AiSmartDispatching>();
            // Appointment Mappings
            CreateMap<Appointment, AppointmentResponseDto>();
            CreateMap<AppointmentCreateDto, Appointment>();
            CreateMap<AppointmentUpdateDto, Appointment>();
            CreateMap<BookAppointmentRequestDto, Appointment>();
            // ApprovalRequest Mappings
            CreateMap<ApprovalRequest, ApprovalRequestResponseDto>();
            CreateMap<ApprovalRequestCreateDto, ApprovalRequest>();
            CreateMap<ApprovalRequestUpdateDto, ApprovalRequest>();
            // AuditLog Mappings
            CreateMap<AuditLog, AuditLogResponseDto>();
            CreateMap<AuditLogCreateDto, AuditLog>();
            CreateMap<AuditLogUpdateDto, AuditLog>();
            // Bay Mappings
            CreateMap<Bay, BayResponseDto>();
            CreateMap<BayCreateDto, Bay>();
            CreateMap<BayUpdateDto, Bay>();
            // Branch Mappings
            CreateMap<Branch, BranchResponseDto>();
            CreateMap<BranchCreateDto, Branch>();
            CreateMap<BranchUpdateDto, Branch>();
            // Team Mappings
            CreateMap<Team, TeamResponseDto>();
            CreateMap<TeamCreateDto, Team>();
            CreateMap<TeamUpdateDto, Team>();
            // BranchException Mappings
            CreateMap<BranchException, BranchExceptionResponseDto>();
            CreateMap<BranchExceptionCreateDto, BranchException>();
            CreateMap<BranchExceptionUpdateDto, BranchException>();
            // BranchOperatingHour Mappings
            CreateMap<BranchOperatingHour, BranchOperatingHourResponseDto>();
            CreateMap<BranchOperatingHourCreateDto, BranchOperatingHour>();
            CreateMap<BranchOperatingHourUpdateDto, BranchOperatingHour>();
            // Category Mappings
            CreateMap<Category, CategoryResponseDto>();
            CreateMap<CategoryCreateDto, Category>();
            CreateMap<CategoryUpdateDto, Category>();
            // ChatMessage Mappings
            CreateMap<ChatMessage, ChatMessageResponseDto>();
            CreateMap<ChatMessageCreateDto, ChatMessage>();
            CreateMap<ChatMessageUpdateDto, ChatMessage>();
            // CommissionRule Mappings
            CreateMap<CommissionRule, CommissionRuleResponseDto>();
            CreateMap<CommissionRuleCreateDto, CommissionRule>();
            CreateMap<CommissionRuleUpdateDto, CommissionRule>();
            // CommissionTransaction Mappings
            CreateMap<CommissionTransaction, CommissionTransactionResponseDto>();
            CreateMap<CommissionTransactionCreateDto, CommissionTransaction>();
            CreateMap<CommissionTransactionUpdateDto, CommissionTransaction>();
            // Conversation Mappings
            CreateMap<Conversation, ConversationResponseDto>();
            CreateMap<ConversationCreateDto, Conversation>();
            CreateMap<ConversationUpdateDto, Conversation>();
            // Customer Mappings
            CreateMap<Customer, CustomerResponseDto>();
            CreateMap<CustomerCreateDto, Customer>()
                .ForMember(dest => dest.PasswordHash, opt => opt.MapFrom(src => src.Password));
            CreateMap<CustomerUpdateDto, Customer>()
                .ForMember(dest => dest.PasswordHash, opt => opt.MapFrom(src => src.Password));
            // CustomerFeedback Mappings
            CreateMap<CustomerFeedback, CustomerFeedbackResponseDto>();
            CreateMap<CustomerFeedbackCreateDto, CustomerFeedback>();
            CreateMap<CustomerFeedbackUpdateDto, CustomerFeedback>();
            // CustomerTier Mappings
            CreateMap<CustomerTier, CustomerTierResponseDto>();
            CreateMap<CustomerTierCreateDto, CustomerTier>();
            CreateMap<CustomerTierUpdateDto, CustomerTier>();
            // CustomerVehicle Mappings
            CreateMap<CustomerVehicle, CustomerVehicleResponseDto>();
            CreateMap<CustomerVehicleCreateDto, CustomerVehicle>();
            CreateMap<CustomerVehicleUpdateDto, CustomerVehicle>();
            // CustomerVoucher Mappings
            CreateMap<CustomerVoucher, CustomerVoucherResponseDto>();
            CreateMap<CustomerVoucherCreateDto, CustomerVoucher>();
            CreateMap<CustomerVoucherUpdateDto, CustomerVoucher>();
            // Delivery Mappings
            CreateMap<Delivery, DeliveryResponseDto>();
            CreateMap<DeliveryCreateDto, Delivery>();
            CreateMap<DeliveryUpdateDto, Delivery>();
            // DiagnosisRecommendation Mappings
            CreateMap<DiagnosisRecommendation, DiagnosisRecommendationResponseDto>();
            CreateMap<DiagnosisRecommendationCreateDto, DiagnosisRecommendation>();
            CreateMap<DiagnosisRecommendationUpdateDto, DiagnosisRecommendation>();
            // Diagnosis Mappings
            CreateMap<Diagnosis, DiagnosisResponseDto>();
            CreateMap<DiagnosisCreateDto, Diagnosis>();
            CreateMap<DiagnosisUpdateDto, Diagnosis>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.ServiceOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore());
            // ExpenseLog Mappings
            CreateMap<ExpenseLog, ExpenseLogResponseDto>();
            CreateMap<ExpenseLogCreateDto, ExpenseLog>();
            CreateMap<ExpenseLogUpdateDto, ExpenseLog>();
            // GatePass Mappings
            CreateMap<GatePass, GatePassResponseDto>();
            CreateMap<GatePassCreateDto, GatePass>();
            CreateMap<GatePassUpdateDto, GatePass>();
            // InsuranceCompany Mappings
            CreateMap<InsuranceCompany, InsuranceCompanyResponseDto>();
            CreateMap<InsuranceCompanyCreateDto, InsuranceCompany>();
            CreateMap<InsuranceCompanyUpdateDto, InsuranceCompany>();
            // Inventory Mappings
            CreateMap<Inventory, InventoryResponseDto>();
            CreateMap<InventoryCreateDto, Inventory>();
            CreateMap<InventoryUpdateDto, Inventory>();
            // InventoryAudit Mappings
            CreateMap<InventoryAudit, InventoryAuditResponseDto>();
            CreateMap<InventoryAuditCreateDto, InventoryAudit>();
            CreateMap<InventoryAuditUpdateDto, InventoryAudit>();
            // InventoryAuditDetail Mappings
            CreateMap<InventoryAuditDetail, InventoryAuditDetailResponseDto>();
            CreateMap<InventoryAuditDetailCreateDto, InventoryAuditDetail>();
            CreateMap<InventoryAuditDetailUpdateDto, InventoryAuditDetail>();
            // Invoice Mappings
            CreateMap<Invoice, InvoiceResponseDto>();
            CreateMap<InvoiceCreateDto, Invoice>();
            CreateMap<InvoiceUpdateDto, Invoice>();
            // InvoiceDetail Mappings
            CreateMap<InvoiceDetail, InvoiceDetailResponseDto>();
            CreateMap<InvoiceDetailCreateDto, InvoiceDetail>();
            CreateMap<InvoiceDetailUpdateDto, InvoiceDetail>();
            // MaterialRequest Mappings
            CreateMap<MaterialRequest, MaterialRequestResponseDto>();
            CreateMap<MaterialRequestCreateDto, MaterialRequest>();
            CreateMap<MaterialRequestUpdateDto, MaterialRequest>();
            // MaterialRequestDetail Mappings
            CreateMap<MaterialRequestDetail, MaterialRequestDetailResponseDto>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : null))
                .ForMember(dest => dest.ProductSku, opt => opt.MapFrom(src => src.Product != null ? src.Product.Sku : null));
            CreateMap<MaterialRequestDetailCreateDto, MaterialRequestDetail>();
            CreateMap<MaterialRequestDetailUpdateDto, MaterialRequestDetail>();
            // MaterialReturn Mappings
            CreateMap<MaterialReturn, MaterialReturnResponseDto>();
            CreateMap<MaterialReturnCreateDto, MaterialReturn>();
            CreateMap<MaterialReturnUpdateDto, MaterialReturn>();
            // Notification Mappings
            CreateMap<Notification, NotificationResponseDto>();
            CreateMap<NotificationCreateDto, Notification>();
            CreateMap<NotificationUpdateDto, Notification>();
            // NotificationTemplate Mappings
            CreateMap<NotificationTemplate, NotificationTemplateResponseDto>();
            CreateMap<NotificationTemplateCreateDto, NotificationTemplate>();
            CreateMap<NotificationTemplateUpdateDto, NotificationTemplate>();
            // Payment Mappings
            CreateMap<Payment, PaymentResponseDto>();
            CreateMap<PaymentCreateDto, Payment>();
            CreateMap<PaymentUpdateDto, Payment>();
            // PaymentMethod Mappings
            CreateMap<PaymentMethod, PaymentMethodResponseDto>();
            CreateMap<PaymentMethodCreateDto, PaymentMethod>();
            CreateMap<PaymentMethodUpdateDto, PaymentMethod>();
            // Product Mappings
            CreateMap<Product, ProductResponseDto>();
            CreateMap<ProductCreateDto, Product>();
            CreateMap<ProductUpdateDto, Product>();
            // ProductImage Mappings
            CreateMap<ProductImage, ProductImageResponseDto>();
            CreateMap<ProductImageCreateDto, ProductImage>();
            CreateMap<ProductImageUpdateDto, ProductImage>();
            // PurchaseOrder Mappings
            CreateMap<PurchaseOrder, PurchaseOrderResponseDto>();
            CreateMap<PurchaseOrderCreateDto, PurchaseOrder>();
            CreateMap<PurchaseOrderUpdateDto, PurchaseOrder>();
            // PurchaseOrderItem Mappings
            CreateMap<PurchaseOrderItem, PurchaseOrderItemResponseDto>();
            CreateMap<PurchaseOrderItemCreateDto, PurchaseOrderItem>();
            CreateMap<PurchaseOrderItemUpdateDto, PurchaseOrderItem>();
            // QcInspection Mappings
            CreateMap<QcInspection, QcInspectionResponseDto>();
            CreateMap<QcInspectionCreateDto, QcInspection>();
            CreateMap<QcInspectionUpdateDto, QcInspection>();
            // RetailOrder Mappings
            CreateMap<RetailOrder, RetailOrderResponseDto>();
            CreateMap<RetailOrderCreateDto, RetailOrder>();
            CreateMap<RetailOrderUpdateDto, RetailOrder>();
            // RetailOrderItem Mappings
            CreateMap<RetailOrderItem, RetailOrderItemResponseDto>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : null))
                .ForMember(dest => dest.ProductSku, opt => opt.MapFrom(src => src.Product != null ? src.Product.Sku : null));
            CreateMap<RetailOrderItemCreateDto, RetailOrderItem>();
            CreateMap<RetailOrderItemUpdateDto, RetailOrderItem>();
            // RetailReturn Mappings
            CreateMap<RetailReturn, RetailReturnResponseDto>();
            CreateMap<RetailReturnCreateDto, RetailReturn>();
            CreateMap<RetailReturnUpdateDto, RetailReturn>();
            // RetailReturnItem Mappings
            CreateMap<RetailReturnItem, RetailReturnItemResponseDto>();
            CreateMap<RetailReturnItemCreateDto, RetailReturnItem>();
            CreateMap<RetailReturnItemUpdateDto, RetailReturnItem>();
            // RevenueSnapshot Mappings
            CreateMap<RevenueSnapshot, RevenueSnapshotResponseDto>();
            CreateMap<RevenueSnapshotCreateDto, RevenueSnapshot>();
            CreateMap<RevenueSnapshotUpdateDto, RevenueSnapshot>();
            // Role Mappings
            CreateMap<Role, RoleResponseDto>();
            CreateMap<RoleCreateDto, Role>();
            CreateMap<RoleUpdateDto, Role>();
            // ServiceCategory Mappings
            CreateMap<ServiceCategory, ServiceCategoryResponseDto>();
            CreateMap<ServiceCategoryCreateDto, ServiceCategory>();
            CreateMap<ServiceCategoryUpdateDto, ServiceCategory>();
            // ServiceOrder Mappings
            CreateMap<ServiceOrder, ServiceOrderResponseDto>();
            CreateMap<ServiceOrderCreateDto, ServiceOrder>();
            CreateMap<ServiceOrderUpdateDto, ServiceOrder>();
            // ServiceOrderDetail Mappings
            CreateMap<ServiceOrderDetail, ServiceOrderDetailResponseDto>();
            CreateMap<ServiceOrderDetailCreateDto, ServiceOrderDetail>();
            CreateMap<ServiceOrderDetailUpdateDto, ServiceOrderDetail>();
            // ServiceOrderDiscount Mappings
            CreateMap<ServiceOrderDiscount, ServiceOrderDiscountResponseDto>();
            CreateMap<ServiceOrderDiscountCreateDto, ServiceOrderDiscount>();
            CreateMap<ServiceOrderDiscountUpdateDto, ServiceOrderDiscount>();
            // ServiceOrderSnapshot Mappings
            CreateMap<ServiceOrderSnapshot, ServiceOrderSnapshotResponseDto>();
            CreateMap<ServiceOrderSnapshotCreateDto, ServiceOrderSnapshot>();
            CreateMap<ServiceOrderSnapshotUpdateDto, ServiceOrderSnapshot>();
            // ServiceRefund Mappings
            CreateMap<ServiceRefund, ServiceRefundResponseDto>();
            CreateMap<ServiceRefundCreateDto, ServiceRefund>();
            CreateMap<ServiceRefundUpdateDto, ServiceRefund>();
            // ServiceStaffAssignment Mappings
            CreateMap<ServiceStaffAssignment, ServiceStaffAssignmentResponseDto>();
            CreateMap<ServiceStaffAssignmentCreateDto, ServiceStaffAssignment>();
            CreateMap<ServiceStaffAssignmentUpdateDto, ServiceStaffAssignment>();
            // Shift Mappings
            CreateMap<Shift, ShiftResponseDto>();
            CreateMap<ShiftCreateDto, Shift>();
            CreateMap<ShiftUpdateDto, Shift>();
            // ShiftClosing Mappings
            CreateMap<ShiftClosing, ShiftClosingResponseDto>();
            CreateMap<ShiftClosingCreateDto, ShiftClosing>();
            CreateMap<ShiftClosingUpdateDto, ShiftClosing>();
            // Staff Mappings
            CreateMap<Staff, StaffResponseDto>();
            CreateMap<StaffCreateDto, Staff>()
                .ForMember(dest => dest.PasswordHash, opt => opt.MapFrom(src => src.Password));
            CreateMap<StaffUpdateDto, Staff>()
                .ForMember(dest => dest.PasswordHash, opt =>
                {
                    opt.PreCondition(src => !string.IsNullOrWhiteSpace(src.Password));
                    opt.MapFrom(src => src.Password);
                })
                .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));
            // StaffSchedule Mappings
            CreateMap<StaffSchedule, StaffScheduleResponseDto>();
            CreateMap<StaffScheduleCreateDto, StaffSchedule>();
            CreateMap<StaffScheduleUpdateDto, StaffSchedule>();
            // StaffTier Mappings
            CreateMap<StaffTier, StaffTierResponseDto>();
            CreateMap<StaffTierCreateDto, StaffTier>();
            CreateMap<StaffTierUpdateDto, StaffTier>();
            // StandardService Mappings
            CreateMap<StandardService, StandardServiceResponseDto>();
            CreateMap<StandardServiceCreateDto, StandardService>();
            CreateMap<StandardServiceUpdateDto, StandardService>();
            // StockTransaction Mappings
            CreateMap<StockTransaction, StockTransactionResponseDto>();
            CreateMap<StockTransactionCreateDto, StockTransaction>();
            CreateMap<StockTransactionUpdateDto, StockTransaction>();
            // Supplier Mappings
            CreateMap<Supplier, SupplierResponseDto>();
            CreateMap<SupplierCreateDto, Supplier>();
            CreateMap<SupplierUpdateDto, Supplier>();
            // Vehicle Mappings
            CreateMap<Vehicle, VehicleResponseDto>();
            CreateMap<VehicleCreateDto, Vehicle>();
            CreateMap<VehicleUpdateDto, Vehicle>();
            // VehicleBrand Mappings
            CreateMap<VehicleBrand, VehicleBrandResponseDto>();
            CreateMap<VehicleBrandCreateDto, VehicleBrand>();
            CreateMap<VehicleBrandUpdateDto, VehicleBrand>();
            // VehicleConditionLog Mappings
            CreateMap<VehicleConditionLog, VehicleConditionLogResponseDto>();
            CreateMap<VehicleConditionLogCreateDto, VehicleConditionLog>();
            CreateMap<VehicleConditionLogUpdateDto, VehicleConditionLog>();
            // VehicleInsurance Mappings
            CreateMap<VehicleInsurance, VehicleInsuranceResponseDto>();
            CreateMap<VehicleInsuranceCreateDto, VehicleInsurance>();
            CreateMap<VehicleInsuranceUpdateDto, VehicleInsurance>();
            // VehicleModel Mappings
            CreateMap<VehicleModel, VehicleModelResponseDto>();
            CreateMap<VehicleModelCreateDto, VehicleModel>();
            CreateMap<VehicleModelUpdateDto, VehicleModel>();
            // Voucher Mappings
            CreateMap<Voucher, VoucherResponseDto>();
            CreateMap<VoucherCreateDto, Voucher>();
            CreateMap<VoucherUpdateDto, Voucher>();
            // Voucher Campaign Mappings
            CreateMap<VoucherCampaign, VoucherCampaignResponseDto>();
            CreateMap<VoucherCampaignCreateDto, VoucherCampaign>();
            CreateMap<VoucherCampaignUpdateDto, VoucherCampaign>();
            // Voucher Approval Mappings
            CreateMap<VoucherApprovalRequest, VoucherApprovalRequestResponseDto>();
            CreateMap<VoucherApprovalRequestCreateDto, VoucherApprovalRequest>();
            // Voucher Redemption Mappings
            CreateMap<VoucherRedemption, VoucherRedemptionResponseDto>();
            // Warehouse Mappings
            CreateMap<Warehouse, WarehouseResponseDto>();
            CreateMap<WarehouseCreateDto, Warehouse>();
            CreateMap<WarehouseUpdateDto, Warehouse>();
            // WorkSlot Mappings
            CreateMap<WorkSlot, WorkSlotResponseDto>();
            CreateMap<WorkSlotCreateDto, WorkSlot>();
            CreateMap<WorkSlotUpdateDto, WorkSlot>();
        }
    }
}
