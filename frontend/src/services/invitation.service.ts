import { apiRequest } from "@/services/api-client";
import type {
  AcceptedInvitation,
  InvitationDetails,
} from "@/types/workspace.types";

export const invitationService = {
  getDetails(token: string, signal?: AbortSignal) {
    return apiRequest<InvitationDetails>(
      `/api/invitations/${encodeURIComponent(token)}`,
      { signal },
    );
  },

  accept(token: string) {
    return apiRequest<AcceptedInvitation>(
      `/api/invitations/${encodeURIComponent(token)}/accept`,
      { method: "POST" },
    );
  },
};
