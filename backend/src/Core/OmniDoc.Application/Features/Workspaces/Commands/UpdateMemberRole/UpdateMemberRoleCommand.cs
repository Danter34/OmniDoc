using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Workspaces.DTOs;
using OmniDoc.Domain.Enums;

namespace OmniDoc.Application.Features.Workspaces.Commands.UpdateMemberRole;

public sealed record UpdateMemberRoleCommand(
    Guid WorkspaceId,
    Guid MemberUserId,
    WorkspaceRole NewRole) : IRequest<Result<WorkspaceMemberDto>>;

public sealed class UpdateMemberRoleCommandHandler
    : IRequestHandler<UpdateMemberRoleCommand, Result<WorkspaceMemberDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public UpdateMemberRoleCommandHandler(
        IApplicationDbContext context,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _context = context;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async Task<Result<WorkspaceMemberDto>> Handle(
        UpdateMemberRoleCommand request,
        CancellationToken cancellationToken)
    {
        var access = await _workspaceAuthorization.AuthorizeAsync(
            request.WorkspaceId,
            WorkspacePermission.ManageRoles,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<WorkspaceMemberDto>.Failure(
                access.Errors,
                access.StatusCode);
        }

        if (!Enum.IsDefined(request.NewRole))
        {
            return Result<WorkspaceMemberDto>.Failure(
                "Workspace role is invalid.",
                400);
        }

        var workspace = await _context.Workspaces
            .Include(item => item.Members)
            .ThenInclude(member => member.User)
            .FirstOrDefaultAsync(item => item.Id == request.WorkspaceId, cancellationToken);

        if (workspace is null)
        {
            return Result<WorkspaceMemberDto>.Failure(
                $"Workspace '{request.WorkspaceId}' was not found.",
                404);
        }

        var targetMember = workspace.Members
            .FirstOrDefault(member => member.UserId == request.MemberUserId);

        if (targetMember is null)
        {
            return Result<WorkspaceMemberDto>.Failure(
                "Workspace member was not found.",
                404);
        }

        var ownershipTransferred = false;
        if (request.NewRole == WorkspaceRole.Owner &&
            workspace.OwnerId != targetMember.UserId)
        {
            var transferAccess = await _workspaceAuthorization.AuthorizeAsync(
                request.WorkspaceId,
                WorkspacePermission.TransferOwnership,
                cancellationToken);

            if (!transferAccess.IsSuccess)
            {
                return Result<WorkspaceMemberDto>.Failure(
                    transferAccess.Errors,
                    transferAccess.StatusCode);
            }

            workspace.OwnerId = targetMember.UserId;
            ownershipTransferred = true;
        }

        if (targetMember.Role == request.NewRole)
        {
            if (ownershipTransferred)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result<WorkspaceMemberDto>.Success(ToDto(targetMember));
        }

        if (targetMember.Role == WorkspaceRole.Owner &&
            request.NewRole != WorkspaceRole.Owner)
        {
            var otherOwner = workspace.Members.FirstOrDefault(member =>
                member.UserId != targetMember.UserId &&
                member.Role == WorkspaceRole.Owner);

            if (otherOwner is null)
            {
                return Result<WorkspaceMemberDto>.Failure(
                    "A workspace must always have at least one owner.",
                    409);
            }

            if (workspace.OwnerId == targetMember.UserId)
            {
                workspace.OwnerId = otherOwner.UserId;
            }
        }

        targetMember.Role = request.NewRole;
        await _context.SaveChangesAsync(cancellationToken);

        return Result<WorkspaceMemberDto>.Success(ToDto(targetMember));
    }

    private static WorkspaceMemberDto ToDto(Domain.Entities.WorkspaceMember member)
    {
        return new WorkspaceMemberDto(
            member.UserId,
            member.User?.FullName ?? string.Empty,
            member.User?.Email ?? string.Empty,
            member.Role.ToString(),
            member.JoinedAtUtc);
    }
}
